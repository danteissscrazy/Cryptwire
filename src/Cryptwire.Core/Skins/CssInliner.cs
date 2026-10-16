using System.Text.RegularExpressions;

namespace Cryptwire.Core.Skins;

/// <summary>El CSS de una piel con sus ficheros ya incrustados, y lo que no se pudo incrustar.</summary>
public sealed record InlinedCss(string Css, IReadOnlyList<string> Skipped);

/// <summary>
/// Incrusta como <c>data:</c> los fondos y fuentes a los que apunta el CSS de una piel.
/// La política de seguridad de WhatsApp Web (CSP) solo admite imágenes y fuentes de su dominio o
/// en <c>data:</c>, así que <c>url("fondo.svg")</c> tiene que llegar a la página ya incrustado.
/// Solo se leen ficheros de imagen o fuente que estén dentro de la carpeta de la piel: una piel
/// no puede colar en la página ningún otro fichero del disco.
/// </summary>
public static partial class CssInliner
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".woff2"] = "font/woff2",
        [".woff"] = "font/woff",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
    };

    /// <param name="css">El CSS de la piel.</param>
    /// <param name="themeDir">La carpeta de la piel, contra la que se resuelven las rutas relativas.</param>
    /// <param name="readFile">Lee un fichero por su ruta completa; null si no existe.</param>
    public static InlinedCss Inline(string css, string themeDir, Func<string, byte[]?> readFile)
    {
        ArgumentNullException.ThrowIfNull(css);
        ArgumentNullException.ThrowIfNull(readFile);
        var root = Path.GetFullPath(themeDir);
        var skipped = new List<string>();

        var result = UrlPattern().Replace(css, match =>
        {
            var reference = match.Groups["path"].Value.Trim();
            if (!IsRelativeFile(reference))
            {
                return match.Value;
            }

            var dataUri = TryBuildDataUri(reference, root, readFile);
            if (dataUri is null)
            {
                skipped.Add(reference);
                return match.Value;
            }

            return $"url(\"{dataUri}\")";
        });

        return new InlinedCss(result, skipped);
    }

    /// <summary>
    /// Una ruta relativa a un fichero. Se dejan tal cual las direcciones con esquema
    /// (<c>https:</c>, <c>data:</c>…), las absolutas y las referencias internas de SVG (<c>#id</c>).
    /// </summary>
    private static bool IsRelativeFile(string reference) =>
        reference.Length > 0
        && reference[0] is not ('#' or '/' or '\\')
        && !reference.Contains(':');

    private static string? TryBuildDataUri(string reference, string root, Func<string, byte[]?> readFile)
    {
        // fuente.woff2?#iefix o fondo.svg#capa: lo que va detrás no es parte del nombre.
        var end = reference.IndexOfAny(['?', '#']);
        var relative = Uri.UnescapeDataString(end >= 0 ? reference[..end] : reference);

        // Una piel mal hecha (o hecha con mala idea) se salta, nunca tumba la app.
        if (relative.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || !MimeTypes.TryGetValue(Path.GetExtension(relative), out var mime))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(root, relative));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var fromRoot = Path.GetRelativePath(root, fullPath);
        if (fromRoot == ".." || fromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(fromRoot))
        {
            return null;
        }

        byte[]? bytes;
        try
        {
            bytes = readFile(fullPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return bytes is null ? null : $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    [GeneratedRegex("""url\(\s*(?<q>['"]?)(?<path>.*?)\k<q>\s*\)""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UrlPattern();
}
