using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Permissions;

/// <summary>
/// Qué permisos del navegador se conceden. A cualquier origen que no sea WhatsApp Web, ninguno.
/// A WhatsApp Web, las notificaciones sin preguntar (son la base de los tonos); el resto
/// (micrófono para notas de voz, cámara…) lo decides tú.
/// </summary>
public sealed class PermissionPolicy
{
    public PermissionDecision Decide(string? origin, bool isNotifications)
    {
        if (!NavigationPolicy.IsWhatsApp(origin))
        {
            return PermissionDecision.Deny;
        }

        return isNotifications ? PermissionDecision.Allow : PermissionDecision.AskUser;
    }
}
