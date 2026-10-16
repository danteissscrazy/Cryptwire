namespace Cryptwire.Core.Skins;

/// <summary>
/// Una piel instalada: una carpeta con su <c>skin.css</c>, sus fondos y fuentes, y si quiere sus tonos
/// (<c>tono.wav</c> para los mensajes, <c>llamada.wav</c> para las llamadas) y un <c>theme.json</c> con el
/// nombre que se enseña y los colores de la ventana. El id es el nombre de la carpeta.
/// </summary>
public sealed record Theme(string Id, string Name, string Directory)
{
    public string CssPath => Path.Combine(Directory, ThemeManager.CssFile);

    /// <summary>El tono de los mensajes. Puede no existir: entonces la piel no suena.</summary>
    public string TonePath => Path.Combine(Directory, ThemeManager.ToneFile);

    /// <summary>El tono de las llamadas, que suena en bucle. Puede no existir.</summary>
    public string CallTonePath => Path.Combine(Directory, ThemeManager.CallToneFile);

    /// <summary>Los colores de la barra de título, los avisos y los ajustes mientras esta piel está puesta.</summary>
    public SkinPalette Palette { get; init; } = SkinPalette.Default;
}
