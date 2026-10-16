namespace Cryptwire.Core.Settings;

/// <summary>
/// Lo que eliges y se guarda entre sesiones. Fase 2: la piel y el modo seguro; los tonos, el fondo y
/// el inicio con Windows llegan en la Fase 3.
/// </summary>
public sealed record AppSettings
{
    /// <summary>La carpeta de la piel elegida. Null: la de por defecto.</summary>
    public string? ThemeId { get; init; }

    /// <summary>Piel apagada: WhatsApp Web tal cual, para cuando una actualización de la web rompa la piel.</summary>
    public bool SafeMode { get; init; }
}
