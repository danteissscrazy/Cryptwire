using Cryptwire.Core.Skins;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// Inyecta el CSS de la piel en cada carga de página. Fase 1: solo aplicar; quitarla (modo seguro)
/// y recargarla en caliente llegan en la Fase 2.
/// </summary>
internal sealed class SkinInjector(CoreWebView2 core)
{
    private string? scriptId;

    public async Task ApplyAsync(string css)
    {
        if (scriptId is not null)
        {
            core.RemoveScriptToExecuteOnDocumentCreated(scriptId);
        }

        scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(SkinScript.Apply(css));
    }
}
