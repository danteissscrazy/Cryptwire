using System.Globalization;
using System.Text;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// El nombre de un chat tal como llega en el título de un aviso, y cuándo dos nombres son el mismo chat.
/// WhatsApp mete a veces marcas invisibles de dirección del texto (alrededor de un teléfono, por ejemplo)
/// y el mismo acento puede llegar en dos formas Unicode: nada de eso cambia de qué chat se trata. Un
/// nombre con medio emoji (un sustituto UTF-16 suelto) tampoco rompe nada.
/// </summary>
public static class ChatName
{
    public const int MaxLength = 100;

    /// <summary>El nombre para guardarlo: sin espacios en los bordes, en forma NFC y no más largo de la cuenta. Null si queda vacío.</summary>
    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var clean = SafeText.WithoutLoneSurrogates(name).Normalize(NormalizationForm.FormC).Trim();
        var text = new StringInfo(clean);
        if (text.LengthInTextElements > MaxLength)
        {
            clean = text.SubstringByTextElements(0, MaxLength).TrimEnd();
        }

        return Key(clean).Length > 0 ? clean : null;
    }

    /// <summary>Si los dos nombres son el mismo chat: sin distinguir mayúsculas, marcas invisibles ni espacios repetidos.</summary>
    public static bool Same(string? a, string? b)
    {
        var key = Key(a);
        return key.Length > 0 && string.Equals(key, Key(b), StringComparison.Ordinal);
    }

    private static string Key(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var key = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var rune in SafeText.WithoutLoneSurrogates(name).Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Format or UnicodeCategory.Control)
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = key.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                key.Append(' ');
                pendingSpace = false;
            }

            key.Append(Rune.ToUpperInvariant(rune).ToString());
        }

        return key.ToString();
    }
}
