using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cryptwire.Core.Settings;

/// <summary>
/// Guarda los ajustes en un JSON de la carpeta del usuario. Un fichero roto o ilegible nunca impide
/// arrancar: se usan los ajustes de por defecto, y el roto se aparta como <c>settings.json.roto</c> antes
/// de que el siguiente guardado lo pise. Uno legible pero con huecos (un <c>null</c> donde va un valor,
/// una coma de más, un comentario) se lee igual y lo que falta toma su valor de serie.
/// </summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    public const string BrokenSuffix = ".roto";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var node = JsonNode.Parse(File.ReadAllText(path), NodeOptions, DocumentOptions);
            DropNulls(node);
            return (node?.Deserialize<AppSettings>(Options) ?? new AppSettings()).Normalized();
        }
        catch (JsonException)
        {
            SetAside();
            return new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
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

    /// <summary>
    /// Quita los <c>null</c> de los objetos: así cada ajuste que falte toma su valor de serie en vez de
    /// tumbar la lectura entera (un <c>bool</c> no admite <c>null</c>). Los de las listas los quita
    /// <see cref="AppSettings.Normalized"/>.
    /// </summary>
    private static void DropNulls(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var name in obj.Where(p => p.Value is null).Select(p => p.Key).ToList())
            {
                obj.Remove(name);
            }

            foreach (var child in obj)
            {
                DropNulls(child.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                DropNulls(item);
            }
        }
    }

    /// <summary>Guarda una copia del fichero roto: tus tonos y reglas siguen ahí para arreglarlo a mano.</summary>
    private void SetAside()
    {
        try
        {
            File.Copy(path, path + BrokenSuffix, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sin copia: los ajustes de serie siguen valiendo para arrancar.
        }
    }
}
