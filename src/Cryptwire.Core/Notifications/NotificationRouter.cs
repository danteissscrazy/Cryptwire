using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// Decide qué suena y qué aviso se muestra cuando WhatsApp Web emite una notificación.
/// Fase 2: un único tono, el de la piel activa, que se pregunta en cada aviso porque la piel puede
/// cambiar con la app abierta. Los tonos por chat llegan con <c>ToneResolver</c> en la Fase 3.
/// </summary>
/// <param name="currentTone">Devuelve la ruta del tono que toca ahora; null o vacío, silencio.</param>
public sealed class NotificationRouter(ISoundPlayer soundPlayer, IToastService toastService, Func<string?> currentTone)
{
    /// <returns>True si el aviso se ha mostrado; false si se descarta porque no viene de WhatsApp Web.</returns>
    public bool Handle(IncomingNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!NavigationPolicy.IsWhatsApp(notification.Origin))
        {
            return false;
        }

        var tonePath = currentTone();
        if (!string.IsNullOrWhiteSpace(tonePath))
        {
            soundPlayer.Play(tonePath);
        }

        toastService.Show(notification);
        return true;
    }
}
