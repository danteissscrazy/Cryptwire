using System.Text.Json;

namespace Cryptwire.Core.Skins;

/// <summary>
/// El JavaScript que coloca o quita el CSS de la piel en la página de WhatsApp Web. Solo toca sus propios
/// <c>&lt;style&gt;</c>: no lee la página, no toca sus datos y no abre ningún canal hacia la app.
/// </summary>
public static class SkinScript
{
    /// <summary>El <c>&lt;style&gt;</c> de la primera capa; los demás, con «-2», «-3»…</summary>
    public const string StyleElementId = "cryptwire-skin";

    /// <summary>Tope de capas (piel, fondo y velo). Al quitar, se buscan hasta aquí.</summary>
    public const int MaxLayers = 4;

    private static readonly string IdLiteral = JsonSerializer.Serialize(StyleElementId);

    /// <summary>Solo el documento principal de WhatsApp Web, nunca marcos ni otras webs.</summary>
    private static readonly string Guard =
        $"if (window.top !== window || location.hostname !== {JsonSerializer.Serialize(Navigation.NavigationPolicy.WhatsAppHost)}) return;";

    /// <summary>
    /// El CSS vigente vive en un solo sitio de la página. Quien monta los estilos lo lee ahí en el momento,
    /// así un montaje pendiente de una carga anterior nunca resucita una piel quitada o vieja.
    /// </summary>
    private const string Slot = "window[Symbol.for(\"cryptwire-skin\")]";

    /// <summary>El id del <c>&lt;style&gt;</c> de la capa <c>i</c> (desde 0), en JavaScript.</summary>
    private static readonly string IdOf = $"(i) => i === 0 ? {IdLiteral} : {IdLiteral} + \"-\" + (i + 1)";

    /// <summary>Una sola capa de CSS.</summary>
    public static string Apply(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        return Apply([css]);
    }

    /// <summary>
    /// Pone cada capa en su propio <c>&lt;style&gt;</c>, en orden, en cuanto existe el árbol, y los vuelve a
    /// mover al final del <c>&lt;head&gt;</c> al terminar el HTML. Cada capa va aparte para que un error en
    /// una (un comentario o una llave sin cerrar en una piel tuya) no se trague las siguientes: el velo
    /// tiene que difuminar aunque la piel esté rota. La web sigue añadiendo hojas de estilo después, así que
    /// la piel no gana la cascada por posición: sus reglas tienen que ganar por especificidad o con
    /// <c>!important</c>. Sirve igual al crearse cada documento que sobre la página ya abierta: si los
    /// estilos ya existen, cambia su contenido (recarga en caliente) sin recargar WhatsApp.
    /// </summary>
    public static string Apply(IReadOnlyList<string> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (layers.Count is 0 or > MaxLayers || layers.Any(layer => layer is null))
        {
            throw new ArgumentException($"Entre 1 y {MaxLayers} capas de CSS.", nameof(layers));
        }

        var layersLiteral = JsonSerializer.Serialize(layers);

        return $$"""
            (() => {
              {{Guard}}
              {{Slot}} = {{layersLiteral}};
              const idOf = {{IdOf}};
              const mount = () => {
                const layers = {{Slot}};
                const count = Array.isArray(layers) ? layers.length : 0;
                for (let i = count; i < {{MaxLayers}}; i++) {
                  const old = document.getElementById(idOf(i));
                  if (old) old.remove();
                }
                if (count === 0) return true;
                const parent = document.head || document.documentElement;
                if (!parent) return false;
                for (let i = 0; i < count; i++) {
                  let style = document.getElementById(idOf(i));
                  if (!style) {
                    style = document.createElement("style");
                    style.id = idOf(i);
                  }
                  if (style.textContent !== layers[i]) style.textContent = layers[i];
                  parent.appendChild(style);
                }
                return true;
              };
              if (!mount()) {
                new MutationObserver((_, observer) => { if (mount()) observer.disconnect(); })
                  .observe(document, { childList: true, subtree: true });
              }
              if (document.readyState === "loading") {
                document.addEventListener("DOMContentLoaded", mount, { once: true });
              }
            })();
            """;
    }

    /// <summary>Quita la piel de la página abierta (modo seguro), sin recargar WhatsApp.</summary>
    public static string Remove() => $$"""
        (() => {
          {{Guard}}
          {{Slot}} = null;
          const idOf = {{IdOf}};
          for (let i = 0; i < {{MaxLayers}}; i++) {
            const style = document.getElementById(idOf(i));
            if (style) style.remove();
          }
        })();
        """;
}
