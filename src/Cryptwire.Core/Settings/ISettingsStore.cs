namespace Cryptwire.Core.Settings;

/// <summary>Dónde se guardan los ajustes. Una interfaz para poder probar sin tocar el disco.</summary>
public interface ISettingsStore
{
    /// <summary>Los ajustes guardados; los de por defecto si no hay o no se pueden leer.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
