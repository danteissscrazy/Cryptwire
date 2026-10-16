namespace Cryptwire.Core.Notifications;

/// <summary>Qué está haciendo Windows cuando llega un aviso (lo que responde <c>SHQueryUserNotificationState</c>).</summary>
public enum DesktopState
{
    /// <summary>Puedes ver un aviso.</summary>
    Available,

    /// <summary>Hay algo a pantalla completa: un juego, una presentación, un vídeo.</summary>
    Busy,

    /// <summary>No molestar o las horas tranquilas de Windows.</summary>
    QuietTime,

    /// <summary>Pantalla bloqueada o salvapantallas: nadie ve el aviso.</summary>
    Away,
}

/// <summary>Lo que hay alrededor de un aviso cuando llega.</summary>
/// <param name="ToastsOn">Tu ajuste de avisos en pantalla.</param>
/// <param name="WindowActive">Cryptwire está delante: WhatsApp ya enseña lo que llega.</param>
/// <param name="Desktop">El estado de Windows.</param>
public sealed record DeliveryContext(bool ToastsOn, bool WindowActive, DesktopState Desktop);

/// <summary>Si suena el tono y si sale el aviso en pantalla.</summary>
public sealed record Delivery(bool PlayTone, bool ShowToast);

/// <summary>
/// Cuándo suena un aviso y cuándo se ve. Un aviso en pantalla solo sale si puedes verlo y no estás ya en
/// Cryptwire. Un mensaje no suena con No molestar ni con algo a pantalla completa; una llamada sí suena
/// a pantalla completa, porque se pierde si no la oyes, pero respeta No molestar.
/// </summary>
public static class DeliveryPolicy
{
    public static Delivery Decide(NotificationKind kind, DeliveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var showToast = context.ToastsOn && !context.WindowActive && context.Desktop == DesktopState.Available;
        var playTone = kind == NotificationKind.Call
            ? context.Desktop != DesktopState.QuietTime
            : context.Desktop is DesktopState.Available or DesktopState.Away;
        return new Delivery(playTone, showToast);
    }
}
