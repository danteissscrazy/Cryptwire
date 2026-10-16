using System.Diagnostics;
using System.Runtime.InteropServices;
using Cryptwire.Core.Notifications;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Captura cada aviso que emite la página, apaga el aviso por defecto de WebView2 (con el sonido
/// de Windows) y entrega una <see cref="IncomingNotification"/> al router, que decide. Lo que hagas con
/// el aviso de Cryptwire (pulsarlo, cerrarlo) vuelve a la página como si fuera el suyo; si la página lo
/// cierra (una llamada que se coge o se corta), el de Cryptwire se va y deja de sonar.
/// </summary>
internal sealed class NotificationInterceptor(NotificationRouter router)
{
    public void Attach(CoreWebView2 core) => core.NotificationReceived += OnNotificationReceived;

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        var notification = e.Notification;
        string? key;
        try
        {
            key = router.Handle(
                new IncomingNotification(e.SenderOrigin, notification.Title, notification.Body, notification.Tag, notification.IconUri),
                clicked: () => Report(notification.ReportClicked),
                dismissed: () => Report(notification.ReportClosed));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Si Cryptwire no sabe tratarlo, que lo enseñe WebView2 a su manera: un aviso nunca se pierde.
            // Solo el tipo de error: nunca el contenido del aviso.
            Trace.TraceWarning($"Cryptwire: aviso no tratado ({ex.GetType().Name}); lo enseña WebView2.");
            return;
        }

        // Siempre nuestro: un aviso que el router descarte (no viene de WhatsApp) no se muestra de ninguna manera.
        e.Handled = true;
        if (key is null)
        {
            return;
        }

        notification.ReportShown();
        notification.CloseRequested += (_, _) => router.Close(key);
    }

    /// <summary>La página puede haber cerrado ya el aviso, o haberse recargado: entonces no hay a quién avisar.</summary>
    private static void Report(Action report)
    {
        try
        {
            report();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
        }
    }
}
