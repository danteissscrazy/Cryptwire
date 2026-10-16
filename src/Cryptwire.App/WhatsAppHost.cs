using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Cryptwire.Core;
using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Permissions;
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
    NavigationPolicy navigation,
    PermissionPolicy permissions,
    NotificationRouter router,
    SkinController skins)
{
    private const int MaxReloadsInARow = 3;
    private static readonly string Home = $"https://{NavigationPolicy.WhatsAppHost}/";

    private CoreWebView2? core;
    private int reloadsInARow;

    /// <summary>El motor del navegador se ha cerrado y la ventana ya no puede mostrar nada.</summary>
    public event EventHandler? EngineStopped;

    public async Task InitializeAsync()
    {
        // La sesión del QR vive en %LOCALAPPDATA%\Cryptwire, nunca junto al ejecutable.
        Directory.CreateDirectory(paths.WebViewDataDir);
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: paths.WebViewDataDir);
        await webView.EnsureCoreWebView2Async(environment);
        core = webView.CoreWebView2;

        Harden(core);
        core.NavigationStarting += OnNavigationStarting;
        core.FrameNavigationStarting += OnFrameNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
        core.PermissionRequested += OnPermissionRequested;
        core.NavigationCompleted += OnNavigationCompleted;
        core.ProcessFailed += OnProcessFailed;

        // Concedido de antemano: WhatsApp no necesita pedirlo y así nunca sale el diálogo de WebView2.
        await core.Profile.SetPermissionStateAsync(
            CoreWebView2PermissionKind.Notifications, Home, CoreWebView2PermissionState.Allow);

        // La piel va antes de abrir WhatsApp para que esté ya en el primer documento.
        await skins.AttachAsync(core);

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
        // Una redirección no es un clic tuyo, aunque empezara con uno.
        var decision = navigation.Decide(e.Uri, e.IsUserInitiated && !e.IsRedirected);
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

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (navigation.DecideFrame(e.Uri) == NavigationDecision.Block)
        {
            e.Cancel = true;
        }
    }

    /// <summary>Nunca se abre una segunda ventana de WebView2: los enlaces pulsados van al navegador.</summary>
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (navigation.DecideNewWindow(e.Uri, e.IsUserInitiated) == NavigationDecision.OpenExternal)
        {
            OpenInBrowser(e.Uri);
        }
    }

    /// <summary>
    /// Protocolos de apps instaladas (<c>ms-settings:</c>, <c>steam:</c>…): nunca. Lo que sí sale de la
    /// app (web y correo) ya lo ha abierto <see cref="OpenInBrowser"/> por su cuenta.
    /// </summary>
    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e) =>
        e.Cancel = true;

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        var decision = permissions.Decide(
            e.Uri, isNotifications: e.PermissionKind == CoreWebView2PermissionKind.Notifications);

        if (decision == PermissionDecision.Allow)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
        else if (decision == PermissionDecision.Deny)
        {
            e.State = CoreWebView2PermissionState.Deny;
        }

        // AskUser: se deja en Default y WebView2 te lo pregunta con su diálogo.
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            reloadsInARow = 0;
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        switch (e.ProcessFailedKind)
        {
            // Si la página se cae, se recarga; si se cae una y otra vez, se deja estar en vez de girar en bucle.
            case CoreWebView2ProcessFailedKind.RenderProcessExited when reloadsInARow < MaxReloadsInARow:
                reloadsInARow++;
                Reload();
                break;
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                EngineStopped?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private static void OpenInBrowser(string url)
    {
        // Se vuelve a parsear para abrir siempre la forma normalizada de la dirección.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            // Sin navegador o programa de correo asociado: no se abre nada y la app sigue.
            Trace.TraceWarning($"Cryptwire: no se ha podido abrir un enlace fuera de la app ({ex.NativeErrorCode}).");
        }
    }
}
