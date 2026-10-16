using System.Text;

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
public static class CssInliner
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
        var result = new StringBuilder(css.Length);

        // Se recorre el CSS a mano para no tocar url(...) dentro de comentarios ni de cadenas
        // (content: "url(x)"), que no son referencias a ficheros.
        var i = 0;
        while (i < css.Length)
        {
            var end = css[i] switch
            {
                '/' when At(css, i + 1, '*') => CommentEnd(css, i),
                '"' or '\'' => StringEnd(css, i, out _),
                _ => -1,
            };

            if (end < 0 && TryReadUrl(css, i, out var urlEnd, out var reference))
            {
                result.Append(Rewrite(css[i..urlEnd], reference, root, readFile, skipped));
                i = urlEnd;
                continue;
            }

            // Cualquier otro carácter pasa tal cual; siempre se avanza al menos uno.
            end = Math.Max(end, i + 1);
            result.Append(css, i, end - i);
            i = end;
        }

        return new InlinedCss(result.ToString(), skipped);
    }

    private static string Rewrite(
        string token, string reference, string root, Func<string, byte[]?> readFile, List<string> skipped)
    {
        if (!IsRelativeFile(reference))
        {
            return token;
        }

        var dataUri = TryBuildDataUri(reference, root, readFile);
        if (dataUri is null)
        {
            skipped.Add(reference);
            return token;
        }

        return $"url(\"{dataUri}\")";
    }

    private static bool At(string css, int index, char c) => index < css.Length && css[index] == c;

    /// <summary>Fin de un comentario <c>/* … */</c>; uno sin cerrar llega hasta el final.</summary>
    private static int CommentEnd(string css, int start)
    {
        var close = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
        return close < 0 ? css.Length : close + 2;
    }

    /// <summary>
    /// Fin de una cadena entre comillas, respetando los escapes. Un salto de línea sin escapar la corta:
    /// entonces, o si se acaba el CSS, <paramref name="closed"/> es false.
    /// </summary>
    private static int StringEnd(string css, int start, out bool closed)
    {
        var quote = css[start];
        closed = false;
        for (var i = start + 1; i < css.Length; i++)
        {
            switch (css[i])
            {
                case '\\':
                    i++;
                    break;
                case '\n' or '\r' or '\f':
                    return i;
                default:
                    if (css[i] == quote)
                    {
                        closed = true;
                        return i + 1;
                    }

                    break;
            }
        }

        return css.Length;
    }

    /// <summary>
    /// Lee un <c>url(…)</c> que empieza en <paramref name="start"/>: con comillas o sin ellas, con
    /// espacios alrededor. No vale si forma parte de otro nombre (<c>mi-url(…)</c>).
    /// </summary>
    private static bool TryReadUrl(string css, int start, out int end, out string reference)
    {
        end = start;
        reference = string.Empty;
        if (string.Compare(css, start, "url(", 0, 4, StringComparison.OrdinalIgnoreCase) != 0
            || (start > 0 && IsNameChar(css[start - 1])))
        {
            return false;
        }

        var i = SkipSpaces(css, start + 4);
        if (i < css.Length && css[i] is '"' or '\'')
        {
            var stringEnd = StringEnd(css, i, out var closed);
            if (!closed)
            {
                return false;
            }

            reference = css[(i + 1)..(stringEnd - 1)];
            i = SkipSpaces(css, stringEnd);
        }
        else
        {
            var close = css.IndexOf(')', i);
            if (close < 0)
            {
                return false;
            }

            reference = css[i..close];
            i = close;
        }

        if (!At(css, i, ')'))
        {
            return false;
        }

        end = i + 1;
        reference = reference.Trim();
        return true;
    }

    private static int SkipSpaces(string css, int i)
    {
        while (i < css.Length && char.IsWhiteSpace(css[i]))
        {
            i++;
        }

        return i;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' or '\\' || c > '\u007F';

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
}
