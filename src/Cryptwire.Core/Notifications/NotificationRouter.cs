using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// Decide qué suena y qué aviso se muestra cuando WhatsApp Web emite una notificación: el tono lo elige
/// <see cref="ToneResolver"/> (del chat, general o de la piel) y cuándo suena o se ve,
/// <see cref="DeliveryPolicy"/>. Una llamada suena en bucle hasta que la atiendes, la cierra WhatsApp o
/// pasa <see cref="MaxRing"/>.
/// </summary>
/// <param name="context">Devuelve, en cada aviso, tus ajustes de avisos y el estado de la ventana y de Windows.</param>
public sealed class NotificationRouter(ISoundPlayer soundPlayer, IToastService toastService, ToneResolver tones, Func<DeliveryContext> context)
{
    /// <summary>Tope de una llamada sonando: WhatsApp deja de llamar antes.</summary>
    public static readonly TimeSpan MaxRing = TimeSpan.FromSeconds(60);

    private long count;

    /// <summary>Los chats que te han avisado en esta sesión, para elegirlos en los ajustes.</summary>
    public RecentChats Recent { get; } = new();

    /// <param name="clicked">Lo que hace la página al pulsar el aviso (abrir ese chat).</param>
    /// <param name="dismissed">Lo que hace la página al descartarlo.</param>
    /// <returns>La clave del aviso, para <see cref="Close"/>; null si se descarta porque no viene de WhatsApp Web.</returns>
    public string? Handle(IncomingNotification notification, Action? clicked = null, Action? dismissed = null)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!NavigationPolicy.IsWhatsApp(notification.Origin))
        {
            return null;
        }

        var key = $"aviso-{++count}";
        var kind = notification.Kind;
        var delivery = DeliveryPolicy.Decide(kind, context());
        if (kind == NotificationKind.Call || notification.Tag?.StartsWith(NotificationBridgeScript.WorkerTagPrefix, StringComparison.Ordinal) != true)
        {
            Recent.Add(notification.Title);
        }

        if (delivery.PlayTone && tones.Resolve(notification) is { } tone && !string.IsNullOrWhiteSpace(tone))
        {
            if (kind == NotificationKind.Call)
            {
                soundPlayer.Ring(key, tone, MaxRing);
            }
            else
            {
                soundPlayer.Play(tone);
            }
        }

        if (delivery.ShowToast)
        {
            toastService.Show(new Toast(key, kind, notification.Title, notification.Body)
            {
                IconUri = NotificationIcon.Classify(notification.IconUri) == IconSource.None ? null : notification.IconUri,
                Clicked = () =>
                {
                    soundPlayer.StopRinging(key);
                    clicked?.Invoke();
                },
                Dismissed = () =>
                {
                    soundPlayer.StopRinging(key);
                    dismissed?.Invoke();
                },
            });
        }

        return key;
    }

    /// <summary>La página ha cerrado el aviso (la llamada se ha cogido o se ha cortado): se calla y se quita.</summary>
    public void Close(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        soundPlayer.StopRinging(key);
        toastService.Close(key);
    }
}
