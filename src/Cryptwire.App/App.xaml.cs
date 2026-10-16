using System.Diagnostics;
using System.IO;
using System.Windows;
using Cryptwire.Core;
using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
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
        var host = new WhatsAppHost(window.WebView, paths, new NavigationPolicy(), router, LoadSkinCss(themeDir));

        window.Loaded += async (_, _) =>
        {
            try
            {
                await host.InitializeAsync();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(
                    window,
                    "Cryptwire necesita el runtime de Microsoft Edge WebView2, que no está instalado.",
                    "Cryptwire",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        };
        window.Show();
    }

    /// <summary>Lee el CSS de la piel e incrusta sus fondos y fuentes. Sin piel, la web se ve tal cual.</summary>
    private static string LoadSkinCss(string themeDir)
    {
        var cssPath = Path.Combine(themeDir, "skin.css");
        if (!File.Exists(cssPath))
        {
            Trace.TraceWarning($"Cryptwire: no hay piel en {cssPath}.");
            return string.Empty;
        }

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
}
