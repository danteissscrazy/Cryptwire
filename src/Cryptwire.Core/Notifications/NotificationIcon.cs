namespace Cryptwire.Core.Notifications;

/// <summary>De dónde sale la foto de un aviso.</summary>
public enum IconSource
{
    /// <summary>Sin foto que valga: se dibuja la inicial del chat.</summary>
    None,

    /// <summary>La foto viene dentro del aviso (<c>data:image/…;base64</c>): no hay que pedir nada.</summary>
    Inline,

    /// <summary>La foto está en los servidores de WhatsApp: se pide igual que la pediría el navegador.</summary>
    WhatsAppServer,
}

/// <summary>
/// La foto que WhatsApp pone en un aviso (el <c>icon</c> de la notificación), y si Cryptwire puede usarla.
/// Solo se aceptan fotos incrustadas o de los servidores de WhatsApp por HTTPS: un aviso nunca puede hacer
/// que la app pida algo a otro sitio de internet o de tu red.
/// </summary>
public static class NotificationIcon
{
    /// <summary>Tope de una foto de aviso: las de perfil en miniatura pesan unos pocos KB.</summary>
    public const int MaxBytes = 1024 * 1024;

    private static readonly string[] WhatsAppDomains = ["whatsapp.net", "whatsapp.com"];

    public static IconSource Classify(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return IconSource.None;
        }

        if (uri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && uri.Contains(";base64,", StringComparison.OrdinalIgnoreCase))
        {
            return IconSource.Inline;
        }

        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps
            && parsed.IsDefaultPort
            && string.IsNullOrEmpty(parsed.UserInfo)
            && WhatsAppDomains.Any(domain => IsSameOrSubdomain(parsed.IdnHost, domain))
                ? IconSource.WhatsAppServer
                : IconSource.None;
    }

    /// <summary>Los bytes de una foto incrustada; null si no es base64 válido o pasa del tope.</summary>
    public static byte[]? DecodeInline(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var comma = uri.IndexOf(',', StringComparison.Ordinal);
        if (Classify(uri) != IconSource.Inline || comma < 0)
        {
            return null;
        }

        var payload = uri[(comma + 1)..];
        if ((long)payload.Length * 3 / 4 > MaxBytes)
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsSameOrSubdomain(string host, string domain) =>
        string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}
