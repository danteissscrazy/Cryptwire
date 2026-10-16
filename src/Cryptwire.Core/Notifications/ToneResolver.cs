using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// Qué tono suena para un aviso: el propio del chat; si no tiene, el general que hayas elegido; si
/// tampoco, el de la piel. Una llamada busca igual, pero entre tonos de llamada. Se pregunta en cada
/// aviso porque los ajustes y la piel pueden cambiar con la app abierta.
/// </summary>
/// <param name="settings">Devuelve los ajustes vigentes.</param>
/// <param name="theme">Devuelve la piel elegida (aunque el modo seguro la tenga apagada); null si no hay.</param>
public sealed class ToneResolver(Func<AppSettings> settings, Func<Theme?> theme)
{
    /// <returns>La ruta del tono, o null para no hacer sonar nada.</returns>
    public string? Resolve(IncomingNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var current = settings();
        var rule = current.ToneRules.FirstOrDefault(r => ChatName.Same(r.Chat, notification.Title));
        return notification.Kind == NotificationKind.Call
            ? Pick(rule?.CallTone, current.CallTone, theme()?.CallTonePath)
            : Pick(rule?.MessageTone, current.MessageTone, theme()?.TonePath);
    }

    private static string? Pick(string? own, ToneChoice general, string? skin)
    {
        if (!string.IsNullOrWhiteSpace(own))
        {
            return own;
        }

        return general.Source switch
        {
            ToneSource.None => null,
            ToneSource.File when !string.IsNullOrWhiteSpace(general.File) => general.File,
            _ => skin,
        };
    }
}
