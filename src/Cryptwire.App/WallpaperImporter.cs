using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cryptwire.Core.Wallpapers;

namespace Cryptwire.App;

/// <summary>
/// Convierte la imagen que eliges en el fondo que guarda Cryptwire: girada como la ves (las fotos del
/// móvil guardan el giro aparte), reducida a 3840 px por el lado largo y recomprimida en JPEG, sin sus
/// metadatos (fecha, cámara, a veces el GPS). Solo se guarda la copia, en la carpeta de fondos.
/// </summary>
internal static class WallpaperImporter
{
    /// <summary>Lo más grande que se acepta para empezar: una foto de móvil o una captura enorme caben.</summary>
    public const long MaxSourceBytes = 40 * 1024 * 1024;

    /// <summary>Más píxeles que esto no es una foto, es una trampa: se rechaza antes de descomprimirla.</summary>
    private const long MaxSourcePixels = 200_000_000;

    private const int MaxSide = 3840;
    private const string Prefix = "fondo-";

    /// <returns>La ruta de la copia.</returns>
    /// <exception cref="WallpaperImportException">Si no es una imagen, es desmesurada o no se puede leer ni guardar.</exception>
    public static string Import(string source, string folder)
    {
        try
        {
            var info = new FileInfo(source);
            if (!info.Exists)
            {
                throw new WallpaperImportException("Esa imagen ya no está.");
            }

            if (info.Length > MaxSourceBytes)
            {
                throw new WallpaperImportException($"Esa imagen pesa más de {MaxSourceBytes / 1024 / 1024} MB.");
            }

            var bytes = Encode(Load(source));
            Directory.CreateDirectory(folder);
            var name = Prefix + Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant() + ".jpg";
            var target = Path.Combine(folder, name);
            File.WriteAllBytes(target, bytes);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new WallpaperImportException("No se ha podido leer la imagen o guardar el fondo (¿disco lleno o carpeta sin permiso?).", e);
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException
            or COMException or OverflowException or OutOfMemoryException)
        {
            throw new WallpaperImportException("Ese fichero no es una imagen que se pueda usar de fondo.", e);
        }
    }

    /// <summary>Borra los fondos viejos de la carpeta; se queda solo el que está en uso.</summary>
    public static void Prune(string folder, string? keep)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(folder, Prefix + "*.jpg"))
        {
            if (!string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Abierto por otro programa: se borrará la próxima vez.
                }
            }
        }
    }

    /// <summary>
    /// Dos pasadas: la primera solo lee la cabecera (tamaño y giro) y rechaza lo desmesurado; la segunda
    /// descomprime ya reducida, sin llegar a tener en memoria la imagen entera a su tamaño original.
    /// </summary>
    private static BitmapSource Load(string source)
    {
        int width, height, orientation;
        using (var header = File.OpenRead(source))
        {
            var frame = BitmapDecoder.Create(header, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            (width, height) = (frame.PixelWidth, frame.PixelHeight);
            orientation = Orientation(frame);
        }

        if (width <= 0 || height <= 0 || (long)width * height > MaxSourcePixels)
        {
            throw new WallpaperImportException("Esa imagen es desmesurada para un fondo.");
        }

        // Desde un stream y no desde una dirección: un «%41» en el nombre del fichero no se lee como «A».
        using var stream = File.OpenRead(source);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (width >= height)
        {
            image.DecodePixelWidth = Math.Min(width, MaxSide);
        }
        else
        {
            image.DecodePixelHeight = Math.Min(height, MaxSide);
        }

        image.EndInit();
        image.Freeze();
        return Upright(image, orientation);
    }

    /// <summary>
    /// JPEG con la calidad más alta que quepa en el tope de los fondos. Sin transparencia: el fondo va
    /// detrás de la conversación, y lo transparente se queda en el negro de la noche.
    /// </summary>
    private static byte[] Encode(BitmapSource image)
    {
        var opaque = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
        foreach (var quality in new[] { 88, 78, 65, 50 })
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(opaque));
            using var memory = new MemoryStream();
            encoder.Save(memory);
            if (memory.Length <= WallpaperCss.MaxBytes)
            {
                return memory.ToArray();
            }
        }

        throw new WallpaperImportException("No he podido dejar esa imagen por debajo del tope de los fondos.");
    }

    /// <summary>El giro que guarda la foto en su EXIF (etiqueta 274). 1 es «tal cual».</summary>
    private static int Orientation(BitmapFrame frame)
    {
        try
        {
            return frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort value ? value : 1;
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException or COMException)
        {
            return 1;
        }
    }

    private static BitmapSource Upright(BitmapSource image, int orientation)
    {
        Transform? turn = orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
            6 => new RotateTransform(90),
            7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
            8 => new RotateTransform(270),
            _ => null,
        };
        return turn is null ? image : new TransformedBitmap(image, turn);
    }
}

/// <summary>Una imagen que no se ha podido usar de fondo, con el motivo para enseñártelo.</summary>
internal sealed class WallpaperImportException : Exception
{
    public WallpaperImportException(string message)
        : base(message)
    {
    }

    public WallpaperImportException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
