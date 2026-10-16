using System.IO;
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

    private const int MaxSide = 3840;
    private const string Prefix = "fondo-";

    /// <returns>La ruta de la copia.</returns>
    /// <exception cref="WallpaperImportException">Si no es una imagen, pesa demasiado o no se puede leer.</exception>
    public static string Import(string source, string folder)
    {
        BitmapSource image;
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

            using var stream = File.OpenRead(source);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            image = Fit(Upright(frame, Orientation(frame)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            throw new WallpaperImportException("Ese fichero no es una imagen que se pueda usar de fondo.", e);
        }

        var bytes = Encode(image);
        Directory.CreateDirectory(folder);
        var name = Prefix + Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant() + ".jpg";
        var target = Path.Combine(folder, name);
        File.WriteAllBytes(target, bytes);
        return target;
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

    private static BitmapSource Fit(BitmapSource image)
    {
        var longest = Math.Max(image.PixelWidth, image.PixelHeight);
        if (longest <= MaxSide)
        {
            return image;
        }

        var scale = (double)MaxSide / longest;
        return new TransformedBitmap(image, new ScaleTransform(scale, scale));
    }

    /// <summary>El giro que guarda la foto en su EXIF (etiqueta 274). 1 es «tal cual».</summary>
    private static int Orientation(BitmapFrame frame)
    {
        try
        {
            return frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort value ? value : 1;
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException)
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
