using System.Windows;
using System.Windows.Media;
using Cryptwire.Core.Skins;

namespace Cryptwire.App;

/// <summary>
/// Los colores de la piel activa como pinceles de la app (<c>Cw.*</c>), para que la barra de título, los
/// avisos, los ajustes y el menú de la bandeja cambien con la piel. En XAML se usan con
/// <c>DynamicResource</c>: al cambiar de piel se sustituyen y todo se repinta solo.
/// </summary>
internal static class PaletteResources
{
    public static SkinPalette Current { get; private set; } = SkinPalette.Default;

    public static event EventHandler? Changed;

    public static void Apply(ResourceDictionary resources, SkinPalette palette)
    {
        Current = palette;
        var background = Parse(palette.Background);
        var text = Parse(palette.Text);
        var accent = Parse(palette.Accent);
        var glow = Parse(palette.Glow);
        var alert = Parse(palette.Alert);

        Set(resources, "Cw.Background", background);
        Set(resources, "Cw.Panel", Parse(palette.Panel));
        Set(resources, "Cw.Text", text);
        Set(resources, "Cw.Soft", Parse(palette.Soft));
        Set(resources, "Cw.Faint", Parse(palette.Faint));
        Set(resources, "Cw.Accent", accent);
        Set(resources, "Cw.Glow", glow);
        Set(resources, "Cw.Alert", alert);

        // Derivados: filos, líneas y estados de los botones, con la transparencia del diseño.
        Set(resources, "Cw.AccentEdge", WithAlpha(accent, 0x47));
        Set(resources, "Cw.AccentSoft", WithAlpha(accent, 0x22));
        Set(resources, "Cw.Line", WithAlpha(text, 0x1A));
        Set(resources, "Cw.GlowHover", WithAlpha(glow, 0x14));
        Set(resources, "Cw.GlowPress", WithAlpha(glow, 0x26));
        Set(resources, "Cw.GlowEdge", WithAlpha(glow, 0x4D));
        Set(resources, "Cw.AlertHover", Mix(alert, Colors.Black, 0.18));
        Set(resources, "Cw.AlertPress", Mix(alert, Colors.Black, 0.36));
        resources["Cw.AccentColor"] = accent;
        resources["Cw.BackgroundColor"] = background;

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Mix(Color color, Color with, double amount) => Color.FromArgb(
        color.A,
        (byte)(color.R + ((with.R - color.R) * amount)),
        (byte)(color.G + ((with.G - color.G) * amount)),
        (byte)(color.B + ((with.B - color.B) * amount)));
}
