using Cryptwire.Core.Settings;

namespace Cryptwire.Core.Skins;

/// <summary>
/// Las reglas de lo que se guarda durante una sesión: piel elegida, modo seguro, ventana y el resto de
/// ajustes. Es el único dueño de los ajustes en memoria, para que dos cambios seguidos nunca se pisen.
/// El modo seguro puede venir de tus ajustes (se recuerda) o de arrancar con <c>--modo-seguro</c> (solo
/// esa vez, nunca se guarda). Cada cambio devuelve los ajustes que hay que guardar.
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

    /// <summary>
    /// Dónde dejar la ventana al abrir: la guardada si es plausible; si no, null (centrada, de serie).
    /// </summary>
    public WindowBounds? StartWindow => Settings.Window is { IsPlausible: true } bounds ? bounds : null;

    /// <summary>Recuerda la ventana al cerrar. Una posición imposible no se guarda.</summary>
    public AppSettings RememberWindow(WindowBounds? bounds) =>
        Settings = Settings with { Window = bounds is { IsPlausible: true } ? bounds : Settings.Window };

    /// <summary>Lo contrario de lo que se ve ahora: si hay modo seguro (venga de donde venga), se quita.</summary>
    public AppSettings ToggleSafeMode()
    {
        var on = !SafeMode;
        sessionSafeMode = false;
        return Settings = Settings with { SafeMode = on };
    }

    /// <summary>
    /// Cualquier otro ajuste (tonos, fondo, avisos, bandeja), cambiado sobre los vigentes. La piel, el
    /// modo seguro y la ventana tienen sus propias reglas y no se tocan por aquí.
    /// </summary>
    public AppSettings Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var current = Settings;
        var next = change(current) ?? current;
        return Settings = next.Normalized() with { ThemeId = current.ThemeId, SafeMode = current.SafeMode, Window = current.Window };
    }
}
