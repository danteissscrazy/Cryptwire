namespace Cryptwire.Core.Notifications;

public interface IToastService
{
    /// <summary>Avisa al usuario de que ha llegado algo.</summary>
    void Show(IncomingNotification notification);
}
