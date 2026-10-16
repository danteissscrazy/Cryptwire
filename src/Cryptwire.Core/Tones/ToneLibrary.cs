using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cryptwire.Core.Tones;

/// <summary>
/// La carpeta de tonos de Cryptwire. Un tono que eliges se copia aquí: así sigue sonando aunque muevas o
/// borres el original, y los ajustes nunca apuntan a otro sitio de tu disco.
/// </summary>
public sealed class ToneLibrary(string directory)
{
    /// <summary>Medio minuto de WAV sin comprimir cabe de sobra; un tono no necesita más.</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Los formatos que reproduce Windows sin instalar nada.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".wav", ".mp3", ".wma", ".m4a", ".aac", ".flac"];

    private const int MaxBaseNameLength = 60;

    public string Directory { get; } = directory;

    /// <summary>
    /// Copia el tono a la carpeta y devuelve la ruta de la copia. Si ya hay uno igual (mismo contenido),
    /// devuelve ese; si hay otro con el mismo nombre, la copia se numera.
    /// </summary>
    /// <exception cref="ToneImportException">Si no es un formato de tono, pesa demasiado o no se puede leer.</exception>
    public string Import(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (!Extensions.Contains(extension))
        {
            throw new ToneImportException($"Ese fichero no es un tono que Windows sepa reproducir ({string.Join(", ", Extensions)}).");
        }

        byte[] bytes;
        try
        {
            var info = new FileInfo(source);
            if (!info.Exists)
            {
                throw new ToneImportException("Ese fichero ya no está.");
            }

            if (info.Length > MaxBytes)
            {
                throw new ToneImportException($"Ese tono pesa más de {MaxBytes / 1024 / 1024} MB. Recórtalo o usa un MP3.");
            }

            bytes = File.ReadAllBytes(source);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ToneImportException("No se ha podido leer ese fichero.", e);
        }

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var baseName = SafeBaseName(Path.GetFileNameWithoutExtension(source));
            var hash = SHA256.HashData(bytes);
            for (var n = 1; ; n++)
            {
                var name = n == 1 ? baseName : $"{baseName}-{n}";
                var target = Path.Combine(Directory, name + extension);
                if (!File.Exists(target))
                {
                    File.WriteAllBytes(target, bytes);
                    return target;
                }

                if (SHA256.HashData(File.ReadAllBytes(target)).AsSpan().SequenceEqual(hash))
                {
                    return target;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ToneImportException("No se ha podido guardar la copia del tono (¿disco lleno o carpeta sin permiso?).", e);
        }
    }

    /// <summary>
    /// Un nombre de fichero que vale en Windows: sin caracteres prohibidos, nombres reservados ni puntos al
    /// final. Sin <c>%</c> (al reproducirlo, <c>%41</c> se leería como «A») ni medio emoji.
    /// </summary>
    public static string SafeBaseName(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new StringBuilder();
        foreach (var c in SafeText.WithoutLoneSurrogates(raw).Normalize(NormalizationForm.FormC))
        {
            clean.Append(invalid.Contains(c) || char.IsControl(c) || c == '%' ? '_' : c);
        }

        var name = clean.ToString().Trim().TrimEnd('.', ' ');
        var text = new StringInfo(name);
        if (text.LengthInTextElements > MaxBaseNameLength)
        {
            name = text.SubstringByTextElements(0, MaxBaseNameLength).TrimEnd('.', ' ');
        }

        var reserved = name.Split('.')[0].ToUpperInvariant();
        if (name.Length == 0 || reserved is "CON" or "PRN" or "AUX" or "NUL"
            || (reserved.Length == 4 && (reserved.StartsWith("COM", StringComparison.Ordinal) || reserved.StartsWith("LPT", StringComparison.Ordinal)) && char.IsAsciiDigit(reserved[3])))
        {
            name = "tono" + (name.Length > 0 ? "-" + name : string.Empty);
        }

        return name;
    }
}

/// <summary>Un tono que no se ha podido importar, con el motivo para enseñártelo.</summary>
public sealed class ToneImportException : Exception
{
    public ToneImportException(string message)
        : base(message)
    {
    }

    public ToneImportException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
