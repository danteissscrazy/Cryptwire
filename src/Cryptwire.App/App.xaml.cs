using System.Windows;
using Cryptwire.Core;
using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Permissions;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Punto de arranque y raíz de composición: aquí se crean los objetos y se conectan a mano.
/// </summary>
public partial class App : Application
{
    /// <summary>Arranca sin piel esta vez, sin cambiar tus ajustes. Para cuando la piel rompa la página.</summary>
    private const string SafeModeArgument = "--modo-seguro";

    private SkinController? skins;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.FromEnvironment();
        var window = new MainWindow();
        var skinController = new SkinController(
            new ThemeManager(ThemeRoots(paths)),
            new JsonSettingsStore(paths.SettingsFile),
            safeModeThisSession: e.Args.Contains(SafeModeArgument, StringComparer.OrdinalIgnoreCase),
            window.Dispatcher);
        skins = skinController;
        var router = new NotificationRouter(new SoundPlayer(), new ToastService(window), () => skinController.Active?.TonePath);
        var host = new WhatsAppHost(
            window.WebView, paths, new NavigationPolicy(), new PermissionPolicy(), router, skinController);

        host.EngineStopped += (_, _) =>
            Fail(window, "El motor del navegador se ha cerrado. Vuelve a abrir Cryptwire.");

        window.Bind(skinController);
        window.Loaded += async (_, _) =>
        {
            try
            {
                await host.InitializeAsync();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                Fail(window, "Cryptwire necesita el runtime de Microsoft Edge WebView2, que no está instalado.");
            }
            catch (Exception ex)
            {
                Fail(window, $"Cryptwire no ha podido arrancar WhatsApp Web.\n\n{ex.Message}");
            }
        };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        skins?.Dispose();
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

    private void Fail(Window window, string message)
    {
        MessageBox.Show(window, message, "Cryptwire", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }
}
