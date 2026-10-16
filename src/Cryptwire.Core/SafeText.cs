using System.Text;

namespace Cryptwire.Core;

/// <summary>
/// Texto que viene de fuera (el título de un aviso, el nombre de un fichero) y puede traer medio emoji: un
/// sustituto UTF-16 suelto. .NET se niega a normalizar ese texto y lanza una excepción; aquí se cambia
/// cada sustituto suelto por el carácter de reemplazo (U+FFFD), que sí se puede tratar.
/// </summary>
public static class SafeText
{
    public static string WithoutLoneSurrogates(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder? fixedUp = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var paired = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
            if (paired)
            {
                fixedUp?.Append(c).Append(text[i + 1]);
                i++;
                continue;
            }

            if (char.IsSurrogate(c))
            {
                fixedUp ??= new StringBuilder(text, 0, i, text.Length);
                fixedUp.Append('\uFFFD');
                continue;
            }

            fixedUp?.Append(c);
        }

        return fixedUp?.ToString() ?? text;
    }
}
