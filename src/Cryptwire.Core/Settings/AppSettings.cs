using System.Text.Json.Serialization;
using Cryptwire.Core.Notifications;

namespace Cryptwire.Core.Settings;

/// <summary>
/// Lo que eliges y se guarda entre sesiones: la piel, el modo seguro y la ventana (Fase 2); los tonos, el
/// fondo, los avisos y la bandeja (Fase 3). El inicio con Windows no está aquí: vive en el registro, que
/// es lo que Windows mira al arrancar.
/// </summary>
/// <remarks>
/// <see cref="ToneRules"/> es una lista, y un record compara las listas por referencia: dos ajustes con
/// las mismas reglas no son <c>==</c>. Compáralos campo a campo.
/// </remarks>
public sealed record AppSettings
{
    /// <summary>La carpeta de la piel elegida. Null: la de por defecto.</summary>
    public string? ThemeId { get; init; }

    /// <summary>Piel apagada: WhatsApp Web tal cual, para cuando una actualización de la web rompa la piel.</summary>
    public bool SafeMode { get; init; }

    /// <summary>Dónde y cómo quedó la ventana al cerrarla. Null: centrada, con su tamaño de serie.</summary>
    public WindowBounds? Window { get; init; }

    /// <summary>El tono de los mensajes de cualquier chat sin tono propio.</summary>
    public ToneChoice MessageTone { get; init; } = ToneChoice.FromSkin;

    /// <summary>El tono que suena mientras te llaman, si el chat no tiene uno propio.</summary>
    public ToneChoice CallTone { get; init; } = ToneChoice.FromSkin;

    /// <summary>Tonos propios por chat; como mucho una regla por chat.</summary>
    public IReadOnlyList<ToneRule> ToneRules { get; init; } = [];

    /// <summary>Tu fondo para la conversación, encima del de la piel. Null: el de la piel.</summary>
    public WallpaperSettings? Wallpaper { get; init; }

    /// <summary>Avisos en pantalla con la estética de Cryptwire. Apagados, solo suena el tono.</summary>
    public bool ShowToasts { get; init; } = true;

    /// <summary>Cerrar la ventana la deja en la bandeja, recibiendo avisos. Para salir, la bandeja › Salir.</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>Ya se te ha avisado una vez de que Cryptwire sigue en la bandeja al cerrar.</summary>
    public bool TrayHintShown { get; init; }

    /// <summary>
    /// Los mismos ajustes sin huecos imposibles: un <c>settings.json</c> editado a mano puede traer
    /// <c>null</c> donde la app espera un valor, o reglas sin chat. Lo que no vale vuelve a su valor de serie.
    /// </summary>
    public AppSettings Normalized()
    {
        var rules = new List<ToneRule>();
        foreach (var rule in ToneRules ?? [])
        {
            if (rule is null || ChatName.Normalize(rule.Chat) is not { Length: > 0 } chat
                || rules.Exists(r => ChatName.Same(r.Chat, chat)))
            {
                continue;
            }

            rules.Add(rule with { Chat = chat, MessageTone = Blank(rule.MessageTone), CallTone = Blank(rule.CallTone) });
        }

        return this with
        {
            MessageTone = MessageTone ?? ToneChoice.FromSkin,
            CallTone = CallTone ?? ToneChoice.FromSkin,
            ToneRules = rules,
            Wallpaper = Wallpaper is { File: { Length: > 0 } } wallpaper ? wallpaper.Clamped() : null,
        };
    }

    /// <summary>
    /// Pone o cambia los tonos de un chat, en el sitio de su regla si ya tenía una. Sin ningún tono, la
    /// regla sobra y se quita.
    /// </summary>
    public AppSettings WithToneRule(string chat, string? messageTone, string? callTone)
    {
        if (ChatName.Normalize(chat) is not { } name)
        {
            return this;
        }

        var rule = new ToneRule(name, Blank(messageTone), Blank(callTone));
        var rules = new List<ToneRule>(ToneRules ?? []);
        var at = rules.FindIndex(r => r is not null && ChatName.Same(r.Chat, name));
        var empty = rule.MessageTone is null && rule.CallTone is null;
        if (at >= 0)
        {
            if (empty)
            {
                rules.RemoveAt(at);
            }
            else
            {
                rules[at] = rule;
            }
        }
        else if (!empty)
        {
            rules.Add(rule);
        }

        return this with { ToneRules = rules };
    }

    public AppSettings WithoutToneRule(string chat) =>
        this with { ToneRules = [.. (ToneRules ?? []).Where(r => r is not null && !ChatName.Same(r.Chat, chat))] };

    private static string? Blank(string? path) => string.IsNullOrWhiteSpace(path) ? null : path;
}

/// <summary>
/// Tu fondo de la conversación: una imagen ya copiada a la carpeta de Cryptwire, y cuánto se oscurece
/// (en %) para que los mensajes se lean encima.
/// </summary>
public sealed record WallpaperSettings(string File, int Dim = WallpaperSettings.DefaultDim)
{
    public const int DefaultDim = 55;
    public const int MaxDim = 90;

    public WallpaperSettings Clamped() => this with { Dim = Math.Clamp(Dim, 0, MaxDim) };
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
