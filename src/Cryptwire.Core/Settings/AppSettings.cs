using System.Text.Json.Serialization;

namespace Cryptwire.Core.Settings;

/// <summary>
/// Lo que eliges y se guarda entre sesiones. Fase 2: la piel, el modo seguro y dónde estaba la ventana;
/// los tonos, el fondo y el inicio con Windows llegan en la Fase 3.
/// </summary>
public sealed record AppSettings
{
    /// <summary>La carpeta de la piel elegida. Null: la de por defecto.</summary>
    public string? ThemeId { get; init; }

    /// <summary>Piel apagada: WhatsApp Web tal cual, para cuando una actualización de la web rompa la piel.</summary>
    public bool SafeMode { get; init; }

    /// <summary>Dónde y cómo quedó la ventana al cerrarla. Null: centrada, con su tamaño de serie.</summary>
    public WindowBounds? Window { get; init; }
}

/// <summary>
/// El rectángulo de la ventana sin maximizar, en píxeles de pantalla, y si estaba maximizada. Es lo que
/// Windows guarda y restaura de una ventana (<c>GetWindowPlacement</c>): si el monitor ya no está, Windows
/// la trae a uno que sí esté.
/// </summary>
public sealed record WindowBounds(int Left, int Top, int Right, int Bottom, bool Maximized)
{
    private const int MinWidth = 200;
    private const int MinHeight = 150;
    private const int MaxSide = 32_000;

    /// <summary>
    /// Si se puede usar: ni diminuta, ni gigante, ni con coordenadas imposibles. Un <c>settings.json</c>
    /// editado a mano o roto no debe poder dejar la ventana invisible.
    /// </summary>
    [JsonIgnore]
    public bool IsPlausible =>
        Right - Left is >= MinWidth and <= MaxSide
        && Bottom - Top is >= MinHeight and <= MaxSide
        && Math.Abs((long)Left) <= MaxSide && Math.Abs((long)Top) <= MaxSide;
}
