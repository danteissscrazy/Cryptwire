using System.Text.Json;

namespace Cryptwire.Core.Skins;

/// <summary>
/// El JavaScript que coloca o quita el CSS de la piel en la página de WhatsApp Web. Solo toca un
/// <c>&lt;style&gt;</c> propio: no lee la página, no toca sus datos y no abre ningún canal hacia la app.
/// </summary>
public static class SkinScript
{
    public const string StyleElementId = "cryptwire-skin";

    private static readonly string IdLiteral = JsonSerializer.Serialize(StyleElementId);

    /// <summary>Solo el documento principal de WhatsApp Web, nunca marcos ni otras webs.</summary>
    private static readonly string Guard =
        $"if (window.top !== window || location.hostname !== {JsonSerializer.Serialize(Navigation.NavigationPolicy.WhatsAppHost)}) return;";

    /// <summary>
    /// El CSS vigente vive en un solo sitio de la página. Quien monta el estilo lo lee ahí en el momento,
    /// así un montaje pendiente de una carga anterior nunca resucita una piel quitada o vieja.
    /// </summary>
    private const string Slot = "window[Symbol.for(\"cryptwire-skin\")]";

    /// <summary>
    /// Pone el estilo en cuanto existe el árbol y lo vuelve a mover al final del <c>&lt;head&gt;</c> al
    /// terminar el HTML. La web sigue añadiendo hojas de estilo después, así que la piel no gana la
    /// cascada por posición: sus reglas tienen que ganar por especificidad o con <c>!important</c>.
    /// Sirve igual al crearse cada documento que sobre la página ya abierta: si el estilo ya existe,
    /// cambia su contenido (recarga en caliente) sin recargar WhatsApp.
    /// </summary>
    public static string Apply(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var cssLiteral = JsonSerializer.Serialize(css);

        return $$"""
            (() => {
              {{Guard}}
              {{Slot}} = {{cssLiteral}};
              const mount = () => {
                const css = {{Slot}};
                let style = document.getElementById({{IdLiteral}});
                if (typeof css !== "string") {
                  if (style) style.remove();
                  return true;
                }
                const parent = document.head || document.documentElement;
                if (!parent) return false;
                if (!style) {
                  style = document.createElement("style");
                  style.id = {{IdLiteral}};
                }
                if (style.textContent !== css) style.textContent = css;
                parent.appendChild(style);
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
          const style = document.getElementById({{IdLiteral}});
          if (style) style.remove();
        })();
        """;
}
