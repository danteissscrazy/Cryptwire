using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>
/// La foto del chat para un aviso. Si viene dentro del aviso se usa tal cual; si está en los servidores de
/// WhatsApp se pide igual que la pediría el navegador para enseñar el aviso, con poco tiempo de espera y un
/// tope de tamaño. Cualquier otra dirección se ignora (<see cref="NotificationIcon"/>). Se recuerdan las
/// últimas: varios mensajes del mismo chat no vuelven a pedirla. Solo en memoria, nunca en disco.
/// </summary>
internal static class AvatarLoader
{
    private const int Remembered = 32;
    private const int DecodeSize = 96;

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(3),
    })
    {
        Timeout = TimeSpan.FromSeconds(5),
        MaxResponseContentBufferSize = NotificationIcon.MaxBytes,
    };

    private static readonly LinkedList<(string Uri, ImageSource Image)> Cache = [];

    /// <returns>La foto, o null si no hay, no vale o no llega a tiempo: entonces se queda la inicial.</returns>
    public static async Task<ImageSource?> LoadAsync(string? uri)
    {
        if (uri is null || NotificationIcon.Classify(uri) is var source && source == IconSource.None)
        {
            return null;
        }

        if (Cache.FirstOrDefault(c => c.Uri == uri) is { Image: { } cached })
        {
            return cached;
        }

        try
        {
            var bytes = source == IconSource.Inline ? NotificationIcon.DecodeInline(uri) : await DownloadAsync(uri);
            if (bytes is null || Decode(bytes) is not { } image)
            {
                return null;
            }

            Cache.AddFirst((uri, image));
            while (Cache.Count > Remembered)
            {
                Cache.RemoveLast();
            }

            return image;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or NotSupportedException
            or InvalidOperationException or ArgumentException or FormatException or System.Runtime.InteropServices.COMException
            or OverflowException)
        {
            return null;
        }
    }

    private static async Task<byte[]?> DownloadAsync(string uri)
    {
        using var response = await Http.GetAsync(uri);
        if (!response.IsSuccessStatusCode
            || response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true
            || response.Content.Headers.ContentLength > NotificationIcon.MaxBytes)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// Dos pasadas, como los fondos: la primera solo lee la cabecera y rechaza lo desmesurado; la segunda
    /// descomprime con el códec del formato que dicen sus bytes (<see cref="NotificationIcon.ImageMime"/>),
    /// no con el que elegiría Windows.
    /// </summary>
    private static ImageSource? Decode(byte[] bytes)
    {
        if (NotificationIcon.ImageMime(bytes) is not { } mime)
        {
            return null;
        }

        using (var header = new MemoryStream(bytes, writable: false))
        {
            var size = Open(mime, header, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            if (!NotificationIcon.FitsSize(size.PixelWidth, size.PixelHeight))
            {
                return null;
            }
        }

        using var stream = new MemoryStream(bytes, writable: false);
        BitmapSource image = Open(mime, stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var scale = (double)DecodeSize / Math.Max(image.PixelWidth, image.PixelHeight);
        if (scale < 1)
        {
            image = new TransformedBitmap(image, new ScaleTransform(scale, scale));
        }

        image.Freeze();
        return image.PixelWidth > 0 && image.PixelHeight > 0 ? image : null;
    }

    private static BitmapDecoder Open(string mime, Stream stream, BitmapCreateOptions options, BitmapCacheOption cache) => mime switch
    {
        "image/png" => new PngBitmapDecoder(stream, options, cache),
        "image/jpeg" => new JpegBitmapDecoder(stream, options, cache),
        // WPF no trae clase propia para WebP; sus bytes ya han dicho que es WebP, y solo ese códec los reconoce.
        _ => BitmapDecoder.Create(stream, options, cache),
    };
}
