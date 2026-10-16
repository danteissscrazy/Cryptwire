using Cryptwire.Core.Notifications;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Captura cada aviso que emite la página, apaga el aviso por defecto de WebView2 (con el sonido
/// de Windows) y entrega una <see cref="IncomingNotification"/> al router, que decide.
/// </summary>
internal sealed class NotificationInterceptor(NotificationRouter router)
{
    public void Attach(CoreWebView2 core) => core.NotificationReceived += OnNotificationReceived;

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        // Siempre nuestro: un aviso que el router descarte no se muestra de ninguna manera.
        e.Handled = true;

        var notification = e.Notification;
        if (router.Handle(new IncomingNotification(e.SenderOrigin, notification.Title, notification.Body)))
        {
            notification.ReportShown();
        }
    }
}
