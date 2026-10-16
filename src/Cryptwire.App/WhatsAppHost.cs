using System.Diagnostics;
using System.IO;
using Cryptwire.Core;
using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Cryptwire.App;

/// <summary>
/// Configura WebView2 para WhatsApp Web: carpeta de sesión, ajustes blindados, navegación,
/// permisos, piel y avisos. Solo traduce eventos: las decisiones las toma Cryptwire.Core.
/// </summary>
internal sealed class WhatsAppHost(
    WebView2 webView,
    AppPaths paths,
    NavigationPolicy policy,
    NotificationRouter router,
    string skinCss)
{
    private static readonly string Home = $"https://{NavigationPolicy.WhatsAppHost}/";

    private CoreWebView2? core;

    public async Task InitializeAsync()
    {
        // La sesión del QR vive en %LOCALAPPDATA%\Cryptwire, nunca junto al ejecutable.
        Directory.CreateDirectory(paths.WebViewDataDir);
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: paths.WebViewDataDir);
        await webView.EnsureCoreWebView2Async(environment);
        core = webView.CoreWebView2;

        Harden(core);
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.PermissionRequested += OnPermissionRequested;
        core.ProcessFailed += OnProcessFailed;

        // Concedido de antemano: WhatsApp no necesita pedirlo y así nunca sale el diálogo de WebView2.
        await core.Profile.SetPermissionStateAsync(
            CoreWebView2PermissionKind.Notifications, Home, CoreWebView2PermissionState.Allow);

        if (skinCss.Length > 0)
        {
            await new SkinInjector(core).ApplyAsync(skinCss);
        }

        new NotificationInterceptor(router).Attach(core);
        core.Navigate(Home);
    }

    public void Reload() => core?.Reload();

    private static void Harden(CoreWebView2 core)
    {
        var settings = core.Settings;
        // Sin puente entre la página y la app: la web no puede llamar a nada nativo.
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.IsStatusBarEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
#endif
        // Cryptwire no guarda contraseñas ni datos de formularios.
        core.Profile.IsPasswordAutosaveEnabled = false;
        core.Profile.IsGeneralAutofillEnabled = false;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var decision = policy.Decide(e.Uri);
        if (decision == NavigationDecision.Allow)
        {
            return;
        }

        e.Cancel = true;
        if (decision == NavigationDecision.OpenExternal)
        {
            OpenInBrowser(e.Uri);
        }
    }

    /// <summary>Nunca se abre una segunda ventana de WebView2: los enlaces van al navegador.</summary>
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (policy.Decide(e.Uri) != NavigationDecision.Block)
        {
            OpenInBrowser(e.Uri);
        }
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (!NavigationPolicy.IsWhatsApp(e.Uri))
        {
            e.State = CoreWebView2PermissionState.Deny;
        }
        else if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }

        // El resto de permisos de WhatsApp (micrófono para notas de voz, cámara…) los pregunta
        // WebView2 con su propio diálogo, y decides tú.
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
        {
            Reload();
        }
    }

    private static void OpenInBrowser(string url)
    {
        // Se vuelve a parsear para abrir siempre la forma normalizada de la dirección.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
