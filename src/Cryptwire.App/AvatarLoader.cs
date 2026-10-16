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
            or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException or OverflowException)
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

    private static ImageSource? Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = DecodeSize;
        image.EndInit();
        image.Freeze();
        return image.PixelWidth > 0 ? image : null;
    }
}
