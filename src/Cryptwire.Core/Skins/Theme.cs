namespace Cryptwire.Core.Skins;

/// <summary>
/// Una piel instalada: una carpeta con su <c>skin.css</c>, sus fondos y fuentes, y si quiere un tono
/// (<c>tono.wav</c>) y un <c>theme.json</c> con el nombre que se enseña. El id es el nombre de la carpeta.
/// </summary>
public sealed record Theme(string Id, string Name, string Directory)
{
    public string CssPath => Path.Combine(Directory, ThemeManager.CssFile);

    /// <summary>El tono de la piel. Puede no existir: entonces la piel no suena.</summary>
    public string TonePath => Path.Combine(Directory, ThemeManager.ToneFile);
}
