using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// Decide qué suena y qué aviso se muestra cuando WhatsApp Web emite una notificación.
/// Fase 1: un único tono, el de la piel. Los tonos por chat llegan con <c>ToneResolver</c> en la Fase 3.
/// </summary>
public sealed class NotificationRouter(ISoundPlayer soundPlayer, IToastService toastService, string? tonePath)
{
    /// <returns>True si el aviso se ha mostrado; false si se descarta porque no viene de WhatsApp Web.</returns>
    public bool Handle(IncomingNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!NavigationPolicy.IsWhatsApp(notification.Origin))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tonePath))
        {
            soundPlayer.Play(tonePath);
        }

        toastService.Show(notification);
        return true;
    }
}
