using System.Runtime.InteropServices;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>Pregunta a Windows si es buen momento para un aviso (<c>SHQueryUserNotificationState</c>).</summary>
internal static class DesktopNotificationState
{
    public static DesktopState Query()
    {
        if (SHQueryUserNotificationState(out var state) != 0)
        {
            return DesktopState.Available;
        }

        return state switch
        {
            1 => DesktopState.Away,       // QUNS_NOT_PRESENT: bloqueado o salvapantallas
            2 or 3 or 4 or 7 => DesktopState.Busy, // pantalla completa, Direct3D, presentación, app a pantalla completa
            6 => DesktopState.QuietTime,  // QUNS_QUIET_TIME: no molestar
            _ => DesktopState.Available,  // QUNS_ACCEPTS_NOTIFICATIONS
        };
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
