using Cryptwire.Core.Wallpapers;

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

    /// <summary>
    /// Lado más largo que se descomprime. La foto se pinta a 96 px; una mucho mayor solo sirve para comerse
    /// la memoria: un PNG de 467 bytes de 1 × 200 000 px pide más de 2 GB al descomprimirlo.
    /// </summary>
    public const int MaxSide = 1024;

    private static readonly string[] WhatsAppDomains = ["whatsapp.net", "whatsapp.com"];

    /// <summary>
    /// El formato de la foto por sus primeros bytes: JPEG, PNG o WebP, como los fondos. Cualquier otro no se
    /// descomprime: Windows elige el códec por el contenido, y la página podría llevar a la app a cualquiera
    /// de los que haya instalados (TIFF, RAW…), fuera del aislamiento del navegador.
    /// </summary>
    public static string? ImageMime(ReadOnlySpan<byte> bytes) => WallpaperCss.ImageMime(bytes);

    /// <summary>Si una foto de esas medidas se puede descomprimir sin peligro.</summary>
    public static bool FitsSize(int width, int height) => width is > 0 and <= MaxSide && height is > 0 and <= MaxSide;

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
