using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cryptwire.Core.Skins;

/// <summary>
/// Los colores de lo que Cryptwire pinta fuera de la web: la barra de título, los avisos, la pantalla de
/// ajustes y el menú de la bandeja. Cada piel los da en su <c>theme.json</c>, dentro de <c>"ventana"</c>;
/// el que falte o no sea un color, sale del Relicario. Formato <c>#RRGGBB</c> o <c>#AARRGGBB</c>.
/// </summary>
/// <param name="Background">fondo: la barra de título y el fondo de las ventanas.</param>
/// <param name="Panel">panel: avisos y paneles de ajustes.</param>
/// <param name="Text">texto: el principal.</param>
/// <param name="Soft">suave: texto secundario y botones de la barra.</param>
/// <param name="Faint">tenue: etiquetas y la piel activa en la barra.</param>
/// <param name="Accent">acento: el oro del Relicario.</param>
/// <param name="Glow">brillo: el hielo del Relicario, para lo activo.</param>
/// <param name="Alert">alerta: modo seguro, cerrar y llamadas.</param>
public sealed partial record SkinPalette(
    string Background,
    string Panel,
    string Text,
    string Soft,
    string Faint,
    string Accent,
    string Glow,
    string Alert)
{
    public const string ManifestKey = "ventana";

    public static SkinPalette Default { get; } = new(
        Background: "#02040A",
        Panel: "#070C19",
        Text: "#EEF1F7",
        Soft: "#9AA3B8",
        Faint: "#677189",
        Accent: "#DCB663",
        Glow: "#45D4FF",
        Alert: "#E0435A");

    /// <summary>Si el texto es un color que entiende la app: <c>#RRGGBB</c> o <c>#AARRGGBB</c>.</summary>
    public static bool IsColor(string? value) => value is not null && ColorPattern().IsMatch(value);

    /// <summary>
    /// La paleta del objeto <c>"ventana"</c> de un <c>theme.json</c>. Lo que no esté o no sea un color
    /// queda como en <see cref="Default"/>; si no hay objeto, la paleta entera es la de serie.
    /// </summary>
    public static SkinPalette FromManifest(JsonElement manifest)
    {
        if (manifest.ValueKind != JsonValueKind.Object
            || !manifest.TryGetProperty(ManifestKey, out var colors)
            || colors.ValueKind != JsonValueKind.Object)
        {
            return Default;
        }

        string Read(string name, string fallback) =>
            colors.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && IsColor(value.GetString())
                ? value.GetString()!.ToUpperInvariant()
                : fallback;

        return new SkinPalette(
            Read("fondo", Default.Background),
            Read("panel", Default.Panel),
            Read("texto", Default.Text),
            Read("suave", Default.Soft),
            Read("tenue", Default.Faint),
            Read("acento", Default.Accent),
            Read("brillo", Default.Glow),
            Read("alerta", Default.Alert));
    }

    [GeneratedRegex("^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
