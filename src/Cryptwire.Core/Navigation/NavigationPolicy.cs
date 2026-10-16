namespace Cryptwire.Core.Navigation;

/// <summary>
/// Decide qué dirección se abre dentro de la ventana, cuál en el navegador y cuál se bloquea.
/// Dentro solo entra WhatsApp Web por HTTPS. Fuera solo salen enlaces web y de correo: cualquier
/// otro esquema (<c>file:</c>, <c>javascript:</c>, protocolos de apps instaladas…) se bloquea,
/// porque abrirlo con el shell de Windows podría lanzar un programa.
/// </summary>
public sealed class NavigationPolicy
{
    public const string WhatsAppHost = "web.whatsapp.com";

    public NavigationDecision Decide(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return NavigationDecision.Block;
        }

        if (IsWhatsApp(uri))
        {
            return NavigationDecision.Allow;
        }

        return uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeMailto
            ? NavigationDecision.OpenExternal
            : NavigationDecision.Block;
    }

    /// <summary>
    /// True solo para <c>https://web.whatsapp.com</c> en su puerto por defecto. Sirve también para
    /// decidir a quién se conceden permisos como las notificaciones.
    /// </summary>
    public static bool IsWhatsApp(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.Equals(uri.IdnHost, WhatsAppHost, StringComparison.OrdinalIgnoreCase);

    public static bool IsWhatsApp(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsWhatsApp(uri);
}
