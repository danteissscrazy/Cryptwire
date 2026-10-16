using Cryptwire.Core.Navigation;
using Cryptwire.Core.Notifications;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Captura cada aviso que emite WhatsApp Web, apaga el aviso por defecto de WebView2 (con el sonido
/// de Windows) y entrega una <see cref="IncomingNotification"/> al router.
/// </summary>
internal sealed class NotificationInterceptor(NotificationRouter router)
{
    public void Attach(CoreWebView2 core) => core.NotificationReceived += OnNotificationReceived;

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        // Lo marcamos como nuestro siempre: un aviso que no venga de WhatsApp no se muestra.
        e.Handled = true;
        if (!NavigationPolicy.IsWhatsApp(e.SenderOrigin))
        {
            return;
        }

        var notification = e.Notification;
        router.Handle(new IncomingNotification(notification.Title, notification.Body));
        notification.ReportShown();
    }
}
