using Cryptwire.Core.Layout;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Skins;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Pone y quita lo que Cryptwire añade a la página: el CSS de la piel (con tu fondo), el divisor ajustable
/// entre la lista y la conversación y el puente que trae a Cryptwire los avisos del service worker. En cada
/// documento que se cree a partir de ahora y en la página que ya está abierta, sin recargar WhatsApp.
/// </summary>
internal sealed class SkinInjector(CoreWebView2 core)
{
    private string? skinScriptId;
    private string? layoutScriptId;
    private string? bridgeScriptId;

    /// <summary>
    /// Pone el puente, el divisor y la piel; sin CSS (no hay piel ni fondo), solo el puente y el divisor. El
    /// script nuevo de la piel se añade antes de quitar el viejo: una navegación justo entre medias nunca se
    /// queda sin piel, y si añadirlo falla, sigue el anterior.
    /// </summary>
    public async Task ApplyAsync(string? css)
    {
        if (bridgeScriptId is null)
        {
            bridgeScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(NotificationBridgeScript.Install());
            await core.ExecuteScriptAsync(NotificationBridgeScript.Install());
        }

        if (layoutScriptId is null)
        {
            layoutScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(LayoutScript.Install());
            await core.ExecuteScriptAsync(LayoutScript.Install());
        }

        if (css is null)
        {
            await RemoveSkinAsync();
            return;
        }

        var script = SkinScript.Apply(css);
        var previous = skinScriptId;
        skinScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        Forget(previous);
        await core.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// Modo seguro: WhatsApp Web tal cual, sin piel, fondo, divisor ni puente. Los avisos con botones (las
    /// llamadas) vuelven a salir como avisos de Windows de los de siempre.
    /// </summary>
    public async Task RemoveAsync()
    {
        await RemoveSkinAsync();
        Forget(layoutScriptId);
        layoutScriptId = null;
        await core.ExecuteScriptAsync(LayoutScript.Remove());
        Forget(bridgeScriptId);
        bridgeScriptId = null;
        await core.ExecuteScriptAsync(NotificationBridgeScript.Remove());
    }

    private async Task RemoveSkinAsync()
    {
        Forget(skinScriptId);
        skinScriptId = null;
        await core.ExecuteScriptAsync(SkinScript.Remove());
    }

    private void Forget(string? id)
    {
        if (id is not null)
        {
            core.RemoveScriptToExecuteOnDocumentCreated(id);
        }
    }
}
