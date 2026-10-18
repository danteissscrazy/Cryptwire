namespace Cryptwire.Core.Navigation;

/// <summary>
/// Decide qué dirección se abre dentro de la ventana, cuál en el navegador y cuál se bloquea.
/// Dentro solo entra WhatsApp Web por HTTPS. Fuera solo salen enlaces web y de correo, y solo si los
/// has pulsado tú: la página no puede abrir tu navegador por su cuenta. Cualquier otro esquema
/// (<c>file:</c>, <c>javascript:</c>, protocolos de apps instaladas…) se bloquea, porque abrirlo con el
/// shell de Windows podría lanzar un programa.
/// </summary>
public sealed class NavigationPolicy
{
    public const string WhatsAppHost = "web.whatsapp.com";

    /// <summary>Navegación del documento principal de la ventana.</summary>
    /// <param name="userInitiated">Si la ha provocado un clic tuyo (y no una redirección ni un script).</param>
    public NavigationDecision Decide(string? url, bool userInitiated)
    {
        var decision = Classify(url);
        return decision == NavigationDecision.OpenExternal && !userInitiated
            ? NavigationDecision.Block
            : decision;
    }

    /// <summary>
    /// Petición de ventana nueva (<c>target="_blank"</c>, <c>window.open</c>). Nunca se abre otra ventana
    /// de WebView2: lo que no está bloqueado va al navegador, y solo si lo has pulsado tú.
    /// </summary>
    public NavigationDecision DecideNewWindow(string? url, bool userInitiated) =>
        userInitiated && Classify(url) != NavigationDecision.Block
            ? NavigationDecision.OpenExternal
            : NavigationDecision.Block;

    /// <summary>
    /// Marcos dentro de la página (los <c>iframe</c> que use WhatsApp): pueden cargar web segura y contenido
    /// propio de la página (<c>about:</c>, <c>blob:</c>, <c>data:</c>). Nunca abren nada fuera de la app.
    /// </summary>
    public NavigationDecision DecideFrame(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "about" or "blob" or "data"
            ? NavigationDecision.Allow
            : NavigationDecision.Block;

    /// <summary>
    /// Descargas: solo las que prepara la propia WhatsApp Web. Lo que bajas de un chat (fotos, vídeos,
    /// documentos) lo descifra la página y lo entrega como <c>blob:https://web.whatsapp.com/…</c>; un
    /// <c>blob:</c> lleva el origen de quien lo creó. Cualquier otra descarga (un marco de otra web, un enlace
    /// directo, un <c>data:</c>) se cancela.
    /// </summary>
    public NavigationDecision DecideDownload(string? url)
    {
        const string blob = "blob:";
        return url is not null
            && url.StartsWith(blob, StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(url[blob.Length..], UriKind.Absolute, out var origin)
            && IsWhatsApp(origin)
                ? NavigationDecision.Allow
                : NavigationDecision.Block;
    }

    /// <summary>
    /// True solo para <c>https://web.whatsapp.com</c> en su puerto por defecto. Sirve también para
    /// decidir a quién se conceden permisos y de quién se aceptan avisos.
    /// </summary>
    public static bool IsWhatsApp(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.Equals(uri.IdnHost, WhatsAppHost, StringComparison.OrdinalIgnoreCase);

    public static bool IsWhatsApp(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsWhatsApp(uri);

    private static readonly string[] MailtoFields = ["to", "cc", "bcc", "subject", "body"];

    /// <summary>
    /// La dirección que se abre fuera de la app, en su forma normalizada, o null si no lo es. De un
    /// <c>mailto:</c> solo quedan los destinatarios, el asunto y el cuerpo: algunos programas de correo
    /// aceptan <c>attach=</c> y pondrían en el borrador un fichero de tu PC.
    /// </summary>
    public static string? ExternalTarget(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeMailto)
        {
            return uri.AbsoluteUri;
        }

        var kept = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(field => MailtoFields.Contains(field.Split('=', 2)[0], StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var address = uri.GetLeftPart(UriPartial.Path);
        return kept.Length > 0 ? $"{address}?{string.Join('&', kept)}" : address;
    }

    private static NavigationDecision Classify(string? url)
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
}
