using System.Text.Json.Serialization;

namespace Cryptwire.Core.Settings;

/// <summary>De dónde sale un tono: el de la piel, ninguno o un fichero tuyo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToneSource>))]
public enum ToneSource
{
    Skin,
    None,
    File,
}

/// <summary>
/// Un tono elegido. Uno de fichero sin fichero vale lo mismo que el de la piel: un ajuste a medias
/// nunca deja la app muda por error.
/// </summary>
public sealed record ToneChoice(ToneSource Source = ToneSource.Skin, string? File = null)
{
    public static ToneChoice FromSkin { get; } = new();

    public static ToneChoice Silent { get; } = new(ToneSource.None);

    public static ToneChoice FromFile(string file) => new(ToneSource.File, file);
}

/// <summary>
/// Tonos propios de un chat. El chat se reconoce por el título del aviso: el nombre del contacto o del
/// grupo tal como lo enseña WhatsApp. Un tono vacío deja el general.
/// </summary>
public sealed record ToneRule(string Chat, string? MessageTone = null, string? CallTone = null);
