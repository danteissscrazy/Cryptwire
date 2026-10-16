using Cryptwire.Core.Skins;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Pone y quita el CSS de la piel: en cada documento que se cree a partir de ahora y en la página
/// que ya está abierta, sin recargar WhatsApp.
/// </summary>
internal sealed class SkinInjector(CoreWebView2 core)
{
    private string? scriptId;

    /// <summary>
    /// El script nuevo se añade antes de quitar el viejo: una navegación justo entre medias nunca se
    /// queda sin piel, y si añadirlo falla, sigue el anterior.
    /// </summary>
    public async Task ApplyAsync(string css)
    {
        var script = SkinScript.Apply(css);
        var previous = scriptId;
        scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        Forget(previous);
        await core.ExecuteScriptAsync(script);
    }

    /// <summary>Modo seguro: WhatsApp Web tal cual.</summary>
    public async Task RemoveAsync()
    {
        Forget(scriptId);
        scriptId = null;
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
