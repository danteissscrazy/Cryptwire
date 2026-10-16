namespace Cryptwire.Core.Notifications;

/// <summary>
/// Un aviso de WhatsApp Web en un formato nuestro, independiente de WebView2.
/// Solo vive en memoria: Cryptwire nunca guarda ni registra el contenido de un aviso.
/// </summary>
/// <param name="Title">Normalmente, el nombre del chat.</param>
/// <param name="Body">El texto del aviso tal como lo muestra WhatsApp.</param>
public sealed record IncomingNotification(string Title, string Body);
