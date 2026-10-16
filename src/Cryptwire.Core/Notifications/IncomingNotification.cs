namespace Cryptwire.Core.Notifications;

/// <summary>
/// Qué avisa: WhatsApp, un mensaje (o algo que suena una vez) o una llamada entrante; o la propia
/// Cryptwire, que solo avisa de sí misma (por ejemplo, que sigue en la bandeja).
/// </summary>
public enum NotificationKind
{
    Message,
    Call,
    App,
}

/// <summary>
/// Un aviso de WhatsApp Web en un formato nuestro, independiente de WebView2.
/// Solo vive en memoria: Cryptwire nunca guarda ni registra el contenido de un aviso.
/// </summary>
/// <param name="Origin">El origen de la página que lo emite, p. ej. <c>https://web.whatsapp.com</c>.</param>
/// <param name="Title">Normalmente, el nombre del chat.</param>
/// <param name="Body">El texto del aviso tal como lo muestra WhatsApp.</param>
/// <param name="Tag">
/// La etiqueta del aviso. Los que llegan por <see cref="NotificationBridgeScript"/> la traen marcada,
/// y así se sabe cuáles son llamadas.
/// </param>
public sealed record IncomingNotification(string Origin, string Title, string Body, string? Tag = null)
{
    public NotificationKind Kind =>
        NotificationBridgeScript.IsCallTag(Tag) ? NotificationKind.Call : NotificationKind.Message;
}
