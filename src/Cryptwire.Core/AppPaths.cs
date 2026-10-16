namespace Cryptwire.Core;

/// <summary>
/// Dónde guarda Cryptwire sus datos. Todo vive en las carpetas del usuario de Windows,
/// nunca junto al ejecutable ni dentro del repo.
/// </summary>
public sealed class AppPaths
{
    private const string AppFolder = "Cryptwire";

    /// <param name="localAppData">La carpeta local del usuario (<c>%LOCALAPPDATA%</c>).</param>
    /// <param name="appBaseDir">La carpeta del ejecutable, de donde se leen las pieles que trae la app.</param>
    /// <exception cref="InvalidOperationException">
    /// Si alguna carpeta llega vacía o relativa: una ruta relativa se resolvería contra el directorio
    /// actual y la sesión de WhatsApp podría acabar dentro del repo.
    /// </exception>
    public AppPaths(string localAppData, string appBaseDir)
    {
        RequireAbsolute(localAppData, nameof(localAppData));
        RequireAbsolute(appBaseDir, nameof(appBaseDir));

        WebViewDataDir = Path.Combine(localAppData, AppFolder, "WebView2");
        ThemesDir = Path.Combine(appBaseDir, "themes");
    }

    /// <summary>Perfil de WebView2: cookies y la sesión vinculada por el QR.</summary>
    public string WebViewDataDir { get; }

    /// <summary>Las pieles que se distribuyen con la app.</summary>
    public string ThemesDir { get; }

    public static AppPaths FromEnvironment() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppContext.BaseDirectory);

    private static void RequireAbsolute(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(
                $"La carpeta '{name}' tiene que ser una ruta absoluta y ha llegado '{path}'.");
        }
    }
}
