using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// Qué tono suena para un aviso: el propio del chat; si no tiene, el general que hayas elegido; si
/// tampoco, el de la piel. Una llamada busca igual, pero entre tonos de llamada, y si la piel no trae
/// <c>llamada.wav</c> suena su tono de mensajes: una llamada nunca se queda muda por falta de fichero.
/// Se pregunta en cada aviso porque los ajustes y la piel pueden cambiar con la app abierta.
/// </summary>
/// <remarks>
/// Un tono tuyo solo vale si está en la carpeta de tonos de Cryptwire (ahí los copia
/// <see cref="Tones.ToneLibrary"/>) y sigue existiendo; si no, se pasa al siguiente. Así un
/// <c>settings.json</c> retocado no puede hacer que la app abra rutas de red o ficheros de otro sitio.
/// </remarks>
/// <param name="settings">Devuelve los ajustes vigentes.</param>
/// <param name="theme">Devuelve la piel elegida (aunque el modo seguro la tenga apagada); null si no hay.</param>
/// <param name="tonesDir">La carpeta de tonos de Cryptwire.</param>
/// <param name="fileExists">Si un fichero existe; para los tests. Por defecto, el disco.</param>
public sealed class ToneResolver(Func<AppSettings> settings, Func<Theme?> theme, string tonesDir, Func<string, bool>? fileExists = null)
{
    private readonly Func<string, bool> exists = fileExists ?? File.Exists;
    private readonly string tones = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tonesDir));

    /// <returns>La ruta del tono, o null para no hacer sonar nada.</returns>
    public string? Resolve(IncomingNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var current = settings();
        var skin = theme();
        var rule = current.ToneRules.FirstOrDefault(r => ChatName.Same(r.Chat, notification.Title));
        return notification.Kind == NotificationKind.Call
            ? Pick(rule?.CallTone, current.CallTone, skin?.CallTonePath, skin?.TonePath)
            : Pick(rule?.MessageTone, current.MessageTone, skin?.TonePath);
    }

    private string? Pick(string? own, ToneChoice general, params string?[] skinTones)
    {
        if (Usable(own))
        {
            return own;
        }

        if (general.Source == ToneSource.None)
        {
            return null;
        }

        if (general.Source == ToneSource.File && Usable(general.File))
        {
            return general.File;
        }

        return skinTones.FirstOrDefault(tone => tone is not null && exists(tone));
    }

    /// <summary>Un tono tuyo: ruta completa, dentro de la carpeta de tonos y todavía en su sitio.</summary>
    private bool Usable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            return ContainedFiles.IsInside(tones, Path.GetFullPath(path)) && exists(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
