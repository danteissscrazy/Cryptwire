using System.Globalization;
using System.Text.Json.Serialization;

namespace Cryptwire.Core.Widgets;

/// <summary>Dónde van los widgets: una barra fina abajo (de serie), un panel a la derecha o ninguno.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WidgetMode>))]
public enum WidgetMode
{
    Bar,
    Panel,
    None,
}

/// <summary>Qué modo de widgets se ve de verdad y cuál viene después con F9.</summary>
public static class WidgetLayout
{
    /// <summary>Por debajo de este ancho (en unidades de WPF), el panel quita demasiado sitio a la conversación.</summary>
    public const double NarrowWidth = 1100;

    /// <summary>F9: barra, panel, ninguno y vuelta a empezar.</summary>
    public static WidgetMode Next(WidgetMode mode) => mode switch
    {
        WidgetMode.Bar => WidgetMode.Panel,
        WidgetMode.Panel => WidgetMode.None,
        _ => WidgetMode.Bar,
    };

    /// <summary>
    /// El modo que se enseña: el elegido, salvo un panel en una ventana estrecha, que pasa a barra si así lo
    /// tienes (con el monitor en vertical, por ejemplo).
    /// </summary>
    public static WidgetMode Effective(WidgetMode chosen, double windowWidth, bool panelToBarWhenNarrow) =>
        chosen == WidgetMode.Panel && panelToBarWhenNarrow && windowWidth < NarrowWidth ? WidgetMode.Bar : chosen;

    /// <summary>Cómo se llama en la barra de título y en los ajustes.</summary>
    public static string Name(WidgetMode mode) => mode switch
    {
        WidgetMode.Panel => "panel",
        WidgetMode.None => "ninguno",
        _ => "barra",
    };
}

/// <summary>
/// Sigilo: una hora sin tonos ni avisos de Cryptwire. Es el «no molestar» de la app, porque el de Windows 11
/// no se deja ver desde fuera. Se guarda la hora a la que acaba, así sobrevive a reabrir la app.
/// </summary>
public static class Stealth
{
    public static readonly TimeSpan Length = TimeSpan.FromHours(1);

    public static bool IsOn(DateTimeOffset? until, DateTimeOffset now) => until is { } end && end > now;

    public static TimeSpan Remaining(DateTimeOffset? until, DateTimeOffset now) =>
        IsOn(until, now) ? until!.Value - now : TimeSpan.Zero;

    /// <summary>Lo que queda, de 1 (recién encendido) a 0 (acabado), para la barrita del panel.</summary>
    public static double Progress(DateTimeOffset? until, DateTimeOffset now) =>
        Math.Clamp(Remaining(until, now) / Length, 0, 1);

    /// <summary>«quedan 45 min», redondeando hacia arriba: con 30 segundos aún queda «1 min».</summary>
    public static string Label(DateTimeOffset? until, DateTimeOffset now)
    {
        var remaining = Remaining(until, now);
        if (remaining <= TimeSpan.Zero)
        {
            return "apagado";
        }

        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        return minutes == 1 ? "queda 1 min" : string.Create(CultureInfo.InvariantCulture, $"quedan {minutes} min");
    }
}
