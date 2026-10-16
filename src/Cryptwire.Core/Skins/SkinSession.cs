using Cryptwire.Core.Settings;

namespace Cryptwire.Core.Skins;

/// <summary>
/// Las reglas de la piel elegida y del modo seguro durante una sesión. El modo seguro puede venir de
/// tus ajustes (se recuerda) o de arrancar con <c>--modo-seguro</c> (solo esa vez, nunca se guarda).
/// Cada cambio devuelve los ajustes que hay que guardar.
/// </summary>
public sealed class SkinSession(AppSettings saved, bool safeModeThisSession)
{
    private bool sessionSafeMode = safeModeThisSession;

    public AppSettings Settings { get; private set; } = saved ?? throw new ArgumentNullException(nameof(saved));

    public bool SafeMode => Settings.SafeMode || sessionSafeMode;

    /// <summary>Elegir una piel es querer verla: también sale del modo seguro, el guardado y el de esta vez.</summary>
    public AppSettings Select(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);
        sessionSafeMode = false;
        return Settings = Settings with { ThemeId = themeId, SafeMode = false };
    }

    /// <summary>Lo contrario de lo que se ve ahora: si hay modo seguro (venga de donde venga), se quita.</summary>
    public AppSettings ToggleSafeMode()
    {
        var on = !SafeMode;
        sessionSafeMode = false;
        return Settings = Settings with { SafeMode = on };
    }
}
