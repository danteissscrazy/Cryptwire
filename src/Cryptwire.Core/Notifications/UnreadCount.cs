using System.Globalization;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// Los mensajes sin leer, sacados del título de la página: WhatsApp Web lo pone como «(3) WhatsApp».
/// Es lo único que Cryptwire lee de la página, y es lo mismo que enseñaría la pestaña de un navegador.
/// </summary>
public static class UnreadCount
{
    /// <summary>Lo que se enseña como mucho; a partir de ahí, «99+».</summary>
    public const int Max = 99;

    /// <returns>El número entre paréntesis al principio del título; 0 si no hay.</returns>
    public static int FromTitle(string? title)
    {
        if (string.IsNullOrEmpty(title) || title[0] != '(')
        {
            return 0;
        }

        var close = title.IndexOf(')', StringComparison.Ordinal);
        if (close < 2)
        {
            return 0;
        }

        var digits = title[1..close].TrimEnd('+');
        return digits.Length > 0
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : 0;
    }

    /// <summary>Cómo se enseña: vacío si no hay, el número, o «99+».</summary>
    public static string Label(int count) => count switch
    {
        <= 0 => string.Empty,
        > Max => $"{Max}+",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };
}
