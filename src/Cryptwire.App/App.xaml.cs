using System.Diagnostics;
using System.IO;
using System.Windows;
using Cryptwire.Core;
using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Permissions;
using Cryptwire.Core.Skins;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Punto de arranque y raíz de composición: aquí se crean los objetos y se conectan a mano.
/// </summary>
public partial class App : Application
{
    /// <summary>Fase 1: una única piel de prueba que viaja con la app.</summary>
    private const string TestTheme = "prueba";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.FromEnvironment();
        var themeDir = Path.Combine(paths.ThemesDir, TestTheme);

        var window = new MainWindow();
        var router = new NotificationRouter(
            new SoundPlayer(),
            new ToastService(window),
            Path.Combine(themeDir, "tono.wav"));
        var host = new WhatsAppHost(
            window.WebView, paths, new NavigationPolicy(), new PermissionPolicy(), router, LoadSkinCss(themeDir));

        host.EngineStopped += (_, _) =>
            Fail(window, "El motor del navegador se ha cerrado. Vuelve a abrir Cryptwire.");

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

    private void Fail(Window window, string message)
    {
        MessageBox.Show(window, message, "Cryptwire", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }

    /// <summary>
    /// Lee el CSS de la piel e incrusta sus fondos y fuentes. Si la piel falta o no se puede leer,
    /// la web se ve tal cual: una piel rota nunca impide abrir WhatsApp.
    /// </summary>
    private static string LoadSkinCss(string themeDir)
    {
        var cssPath = Path.Combine(themeDir, "skin.css");
        try
        {
            var inlined = CssInliner.Inline(
                File.ReadAllText(cssPath),
                themeDir,
                path => File.Exists(path) ? File.ReadAllBytes(path) : null);

            foreach (var reference in inlined.Skipped)
            {
                Trace.TraceWarning($"Cryptwire: la piel apunta a '{reference}' y no se ha podido incrustar.");
            }

            return inlined.Css;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Cryptwire: no se ha podido leer la piel ({ex.GetType().Name}); se muestra la web sin piel.");
            return string.Empty;
        }
    }
}
