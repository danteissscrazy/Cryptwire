using System.Windows;
using Cryptwire.Core;
using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Permissions;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;
using Cryptwire.Core.Startup;
using Cryptwire.Core.Widgets;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Punto de arranque y raíz de composición: aquí se crean los objetos y se conectan a mano. La app vive
/// mientras viva el icono de la bandeja; cerrar la ventana solo la esconde, si así lo tienes elegido.
/// </summary>
public partial class App : Application
{
    /// <summary>Arranca sin piel esta vez, sin cambiar tus ajustes. Para cuando la piel rompa la página.</summary>
    private const string SafeModeArgument = "--modo-seguro";

    /// <summary>Cierra del todo la Cryptwire que esté abierta (también desde la bandeja), sin abrir otra.</summary>
    private const string ExitArgument = "--salir";

    private SingleInstance? instance;
    private SkinController? skins;
    private TrayIcon? tray;
    private MainWindow? window;
    private SettingsWindow? settingsWindow;
    private NotificationRouter? router;
    private AppPaths? paths;
    private int unread;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        instance = new SingleInstance();
        var exitOnly = e.Args.Contains(ExitArgument, StringComparer.OrdinalIgnoreCase);
        var startInTray = e.Args.Contains(StartupCommand.TrayArgument, StringComparer.OrdinalIgnoreCase);
        if (!instance.IsFirst || exitOnly)
        {
            // Ya hay una Cryptwire abierta: que se enseñe ella (o que se cierre, con --salir). Si la
            // segunda viene del inicio de Windows (--bandeja), no hace nada: la primera ya está en su sitio.
            if (!instance.IsFirst && exitOnly)
            {
                instance.AskFirstToExit();
            }
            else if (!instance.IsFirst && !startInTray)
            {
                instance.AskFirstToShow();
            }

            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        PaletteResources.Apply(Resources, SkinPalette.Default);
        DispatcherUnhandledException += OnUnhandledException;

        paths = AppPaths.FromEnvironment();
        var main = new MainWindow();
        window = main;
        var skinController = new SkinController(
            new ThemeManager(ThemeRoots(paths)),
            new JsonSettingsStore(paths.SettingsFile),
            paths.WallpapersDir,
            safeModeThisSession: e.Args.Contains(SafeModeArgument, StringComparer.OrdinalIgnoreCase),
            main.Dispatcher);
        skins = skinController;

        var toasts = new ToastPresenter(main, main.BringToFront);
        router = new NotificationRouter(
            new SoundPlayer(),
            toasts,
            new ToneResolver(() => skinController.Settings, () => skinController.Active, paths.TonesDir),
            () => new DeliveryContext(
                skinController.Settings.ShowToasts,
                WindowActive: main.IsVisible && main.IsActive && main.WindowState != WindowState.Minimized,
                DesktopNotificationState.Query(),
                Silenced: Stealth.IsOn(skinController.Settings.SilencedUntil, DateTimeOffset.Now)));
        var host = new WhatsAppHost(
            main.WebView, paths, new NavigationPolicy(), new PermissionPolicy(), router, skinController);
        var widgets = new WidgetsViewModel(skinController, main.Dispatcher);
        widgets.Ticked += (_, _) => ShowSkinState();

        tray = new TrayIcon();
        tray.OpenRequested += (_, _) => main.BringToFront();
        tray.SettingsRequested += (_, _) => ShowSettings();
        tray.SafeModeRequested += async (_, _) => await skinController.ToggleSafeModeAsync();
        tray.StealthRequested += (_, _) => widgets.StealthOn = !widgets.StealthOn;
        tray.ExitRequested += (_, _) => ExitApp();

        skinController.StateChanged += (_, _) => ShowSkinState();
        host.UnreadChanged += (_, count) =>
        {
            unread = count;
            main.ShowUnread(count);
            widgets.Unread = count;
            ShowSkinState();
        };
        host.EngineStopped += (_, _) =>
            Fail(main, "El motor del navegador se ha cerrado. Vuelve a abrir Cryptwire.");

        main.Bind(skinController);
        main.ShowWidgets(widgets);
        main.WidgetModeRequested += async (_, _) =>
            await skinController.UpdateAsync(s => s with { Widgets = WidgetLayout.Next(s.Widgets) }, refresh: false);
        main.CloseChatRequested += async (_, _) => await host.CloseChatAsync();
        main.SettingsRequested += (_, _) => ShowSettings();
        main.HideOnClose = () => skinController.Settings.CloseToTray;
        main.HiddenToTray += async (_, _) => await HintTrayOnceAsync(toasts);

        // Cerrada de verdad (sin bandeja, o desde Salir): la app termina con ella.
        main.Closed += (_, _) => Shutdown();
        instance.Listen(
            show: () => main.Dispatcher.InvokeAsync(main.BringToFront),
            exit: () => main.Dispatcher.InvokeAsync(ExitApp));
        SessionEnding += (_, _) => ExitApp();
        ShowSkinState();

        if (startInTray)
        {
            // Al iniciar Windows: WhatsApp arranca por detrás, sin ventana, y avisa desde la bandeja.
            main.Loaded += async (_, _) =>
            {
                await StartAsync(host, main);
                main.FinishHiddenStart();
            };
            main.StartHidden();
        }
        else
        {
            main.Loaded += async (_, _) => await StartAsync(host, main);
            main.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        skins?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Las pieles de la app y luego las tuyas, que ganan si se llaman igual. En Debug,
    /// <c>CRYPTWIRE_THEMES_DIR</c> añade una carpeta más (la del repo) para editar una piel y verla
    /// cambiar al guardar.
    /// </summary>
    private static List<string> ThemeRoots(AppPaths paths)
    {
        List<string> roots = [paths.ThemesDir, paths.UserThemesDir];
#if DEBUG
        var dev = Environment.GetEnvironmentVariable("CRYPTWIRE_THEMES_DIR");
        if (!string.IsNullOrWhiteSpace(dev) && System.IO.Path.IsPathFullyQualified(dev))
        {
            roots.Add(dev);
        }
#endif
        return roots;
    }

    private async Task StartAsync(WhatsAppHost host, MainWindow main)
    {
        try
        {
            await host.InitializeAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            Fail(main, "Cryptwire necesita el runtime de Microsoft Edge WebView2, que no está instalado.");
        }
        catch (Exception ex)
        {
            Fail(main, $"Cryptwire no ha podido arrancar WhatsApp Web.\n\n{ex.Message}");
        }
    }

    /// <summary>Los colores de la piel en la ventana, los avisos, los ajustes y la bandeja.</summary>
    private void ShowSkinState()
    {
        if (skins is null)
        {
            return;
        }

        var palette = skins.Active?.Palette ?? SkinPalette.Default;
        if (palette != PaletteResources.Current)
        {
            PaletteResources.Apply(Resources, palette);
            tray?.ApplyPalette(palette);
        }

        tray?.Update(skins.SafeMode, Stealth.IsOn(skins.Settings.SilencedUntil, DateTimeOffset.Now), unread);
    }

    /// <summary>La primera vez que cierras la ventana, un aviso de que Cryptwire sigue en la bandeja.</summary>
    private async Task HintTrayOnceAsync(IToastService toasts)
    {
        if (skins is null || skins.Settings.TrayHintShown)
        {
            return;
        }

        toasts.Show(new Toast("cryptwire-bandeja", NotificationKind.App, "Cryptwire sigue aquí",
            "Recibes los avisos desde la bandeja. Para salir del todo: clic derecho en el icono › Salir.")
        {
            Clicked = () => window?.BringToFront(),
        });
        await skins.UpdateAsync(s => s with { TrayHintShown = true }, refresh: false);
    }

    private void ShowSettings()
    {
        if (window is null || window.IsClosed || skins is null || router is null || paths is null)
        {
            return;
        }

        if (settingsWindow is { IsLoaded: true })
        {
            settingsWindow.Activate();
            return;
        }

        window.BringToFront();
        settingsWindow = new SettingsWindow(new SettingsViewModel(skins, router.Recent, paths)) { Owner = window };
        settingsWindow.Show();
    }

    private void ExitApp()
    {
        window?.Exit();
        Shutdown();
    }

    /// <summary>
    /// Último recurso: un error que nadie ha previsto (un disco lleno, una imagen imposible) se enseña y la
    /// app sigue, en vez de cerrarse con WhatsApp dentro. Solo el mensaje del error, nunca el de un aviso.
    /// </summary>
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception is OutOfMemoryException)
        {
            return;
        }

        e.Handled = true;
        System.Diagnostics.Trace.TraceError($"Cryptwire: error no previsto ({e.Exception.GetType().Name}).");
        MessageBox.Show(
            $"Algo no ha salido bien, pero Cryptwire sigue abierta.\n\n{e.Exception.Message}",
            "Cryptwire",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void Fail(Window owner, string message)
    {
        MessageBox.Show(owner, message, "Cryptwire", MessageBoxButton.OK, MessageBoxImage.Error);
        ExitApp();
    }
}
