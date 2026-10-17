using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Cryptwire.Core.Skins;

/// <summary>
/// Encuentra las pieles en disco, elige cuál toca y monta su CSS final con los fondos y fuentes
/// incrustados. Lee el disco cada vez que se le pide: así una piel nueva o editada aparece sin
/// reiniciar la app.
/// </summary>
public sealed class ThemeManager
{
    public const string CssFile = "skin.css";
    public const string ToneFile = "tono.wav";
    public const string CallToneFile = "llamada.wav";
    public const string ManifestFile = "theme.json";

    /// <summary>La piel que se usa si no has elegido otra, o si la que elegiste ya no está.</summary>
    public const string DefaultThemeId = "relicario";

    /// <summary>Un fichero de la piel más grande que esto no se incrusta (un fondo 4K cabe de sobra).</summary>
    public const long MaxAssetBytes = 8 * 1024 * 1024;

    /// <summary>Tope de todo lo que una piel incrusta, contando cada vez que se usa un fichero.</summary>
    public const long MaxInlinedBytes = 32 * 1024 * 1024;

    public const long MaxCssBytes = 2 * 1024 * 1024;

    private const int MaxNameLength = 40;
    private const int MaxManifestBytes = 16 * 1024;

    private readonly string[] roots;

    /// <param name="roots">
    /// Carpetas de pieles, de menos a más prioridad: si dos tienen una piel con el mismo nombre de
    /// carpeta, gana la de la carpeta posterior (así puedes retocar una copia de una piel de la app).
    /// </param>
    public ThemeManager(IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        this.roots = [.. roots];
        foreach (var root in this.roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            {
                throw new ArgumentException($"La carpeta de pieles tiene que ser una ruta absoluta y ha llegado '{root}'.", nameof(roots));
            }
        }
    }

    /// <summary>Todas las pieles con <c>skin.css</c>, ordenadas por nombre. Una carpeta ilegible se salta.</summary>
    public IReadOnlyList<Theme> LoadAll()
    {
        var found = new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            foreach (var dir in SafeEnumerateDirectories(root))
            {
                if (!File.Exists(Path.Combine(dir, CssFile)))
                {
                    continue;
                }

                var id = Path.GetFileName(dir);
                var (name, palette) = ReadManifest(dir);
                found[id] = new Theme(id, name ?? id, dir) { Palette = palette };
            }
        }

        return [.. found.Values.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// La piel con ese id. Si ya no existe, la de por defecto; si tampoco, la primera. Null si no hay
    /// ninguna: WhatsApp se ve tal cual.
    /// </summary>
    public static Theme? Resolve(IReadOnlyList<Theme> themes, string? id)
    {
        ArgumentNullException.ThrowIfNull(themes);
        return Find(themes, id) ?? Find(themes, DefaultThemeId) ?? themes.FirstOrDefault();
    }

    /// <summary>La siguiente piel de la lista, dando la vuelta al final.</summary>
    public static Theme? Next(IReadOnlyList<Theme> themes, string? currentId)
    {
        ArgumentNullException.ThrowIfNull(themes);
        if (themes.Count == 0)
        {
            return null;
        }

        var index = -1;
        for (var i = 0; i < themes.Count; i++)
        {
            if (string.Equals(themes[i].Id, currentId, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        return themes[(index + 1) % themes.Count];
    }

    /// <summary>
    /// El CSS de la piel con sus fondos y fuentes incrustados. Un fichero que no existe, que pasa de
    /// <see cref="MaxAssetBytes"/>, que agotaría <see cref="MaxInlinedBytes"/> o al que se llega a través
    /// de un enlace (unión o enlace simbólico) se salta y aparece en <see cref="InlinedCss.Skipped"/>.
    /// </summary>
    /// <exception cref="IOException">Si el <c>skin.css</c> no se puede leer, es demasiado grande o es un enlace.</exception>
    /// <exception cref="UnauthorizedAccessException">Si no hay permiso para leerlo.</exception>
    public static InlinedCss BuildCss(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var root = Path.GetFullPath(theme.Directory);
        var css = new FileInfo(theme.CssPath);
        if (css.Exists && css.Length > MaxCssBytes)
        {
            throw new IOException($"El skin.css de la piel '{theme.Id}' pasa de {MaxCssBytes / 1024 / 1024} MB.");
        }

        if (css.Exists && CrossesLink(root, css.FullName))
        {
            throw new IOException($"El skin.css de la piel '{theme.Id}' es un enlace a otro sitio.");
        }

        var read = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        long inlined = 0;
        return CssInliner.Inline(File.ReadAllText(css.FullName), root, path =>
        {
            if (!read.TryGetValue(path, out var bytes))
            {
                bytes = ReadAsset(root, path);
                read[path] = bytes;
            }

            if (bytes is null || inlined + bytes.Length > MaxInlinedBytes)
            {
                return null;
            }

            inlined += bytes.Length;
            return bytes;
        });
    }

    /// <summary>
    /// Si un tono de la piel se puede abrir: un fichero de verdad, no un enlace (uno a una carpeta de red
    /// haría que cada aviso le mandara tu usuario de Windows), y un WAV por sus bytes, no por su nombre
    /// (otro formato acabaría en cualquier códec instalado). La piel puede venir de internet.
    /// </summary>
    public static bool IsWaveFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[12];
            using var file = info.OpenRead();
            return file.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) == header.Length
                && header[..4].SequenceEqual("RIFF"u8)
                && header[8..].SequenceEqual("WAVE"u8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static byte[]? ReadAsset(string root, string path)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length <= MaxAssetBytes && !CrossesLink(root, path)
            ? File.ReadAllBytes(path)
            : null;
    }

    /// <summary>
    /// Si el camino de <paramref name="root"/> a <paramref name="path"/> pasa por un enlace. Que la ruta
    /// esté dentro de la carpeta solo lo dice el texto, y Windows sigue uniones y enlaces simbólicos: uno
    /// dentro de la piel podría apuntar a cualquier fichero del disco. La carpeta de la piel en sí no se
    /// mira: si es un enlace, lo has puesto tú.
    /// </summary>
    private static bool CrossesLink(string root, string path)
    {
        for (var current = path; current is not null && current.Length > root.Length; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Theme? Find(IReadOnlyList<Theme> themes, string? id) =>
        id is null ? null : themes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.Exists(root) ? Directory.GetDirectories(root) : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Deja solo caracteres que se ven: fuera los de control, los de formato (invisibles o que invierten
    /// el sentido del texto) y los separadores de línea y párrafo. Se corta por caracteres completos,
    /// para no partir un emoji ni una letra con tilde compuesta.
    /// </summary>
    private static string CleanName(string raw)
    {
        var kept = new StringBuilder(raw.Length);
        foreach (var rune in raw.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned))
            {
                kept.Append(rune.ToString());
            }
        }

        var clean = kept.ToString().Trim();
        var text = new StringInfo(clean);
        return text.LengthInTextElements > MaxNameLength
            ? text.SubstringByTextElements(0, MaxNameLength).TrimEnd()
            : clean;
    }

    /// <summary>
    /// El nombre y los colores del <c>theme.json</c>. El nombre, limpio: una línea, sin caracteres de
    /// control y corto, porque se enseña en la barra de título y en el menú; null si no hay o no vale.
    /// Los colores, los de la piel o los de serie (<see cref="SkinPalette.FromManifest"/>).
    /// </summary>
    private static (string? Name, SkinPalette Palette) ReadManifest(string dir)
    {
        var manifest = Path.Combine(dir, ManifestFile);
        try
        {
            var info = new FileInfo(manifest);
            if (!info.Exists || info.Length > MaxManifestBytes || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return (null, SkinPalette.Default);
            }

            using var json = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = json.RootElement;
            var palette = SkinPalette.FromManifest(root);
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name)
                || name.ValueKind != JsonValueKind.String)
            {
                return (null, palette);
            }

            var clean = CleanName(name.GetString()!);
            return (clean.Length > 0 ? clean : null, palette);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, SkinPalette.Default);
        }
    }
}
