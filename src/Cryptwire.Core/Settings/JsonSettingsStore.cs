using System.Text.Json;

namespace Cryptwire.Core.Settings;

/// <summary>
/// Guarda los ajustes en un JSON de la carpeta del usuario. Un fichero roto o ilegible nunca impide
/// arrancar: se usan los ajustes de por defecto.
/// </summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Escribe primero en un fichero temporal y luego lo cambia por el bueno: si la app se cierra a
    /// mitad, el fichero anterior sigue entero.
    /// </summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, path, overwrite: true);
    }
}
