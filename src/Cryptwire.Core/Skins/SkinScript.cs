using System.Text.Json;

namespace Cryptwire.Core.Skins;

/// <summary>
/// El JavaScript que coloca el CSS de la piel en la página de WhatsApp Web. Solo añade un
/// <c>&lt;style&gt;</c>: no lee la página, no toca sus datos y no abre ningún canal hacia la app.
/// </summary>
public static class SkinScript
{
    public const string StyleElementId = "cryptwire-skin";

    /// <summary>
    /// Script para ejecutar al crearse cada documento. Pone el estilo en cuanto existe el árbol y
    /// lo vuelve a mover al final al terminar de cargar, para ganar en cascada a los estilos de la
    /// web. Solo actúa en el documento principal de WhatsApp Web, nunca en marcos ni en otras webs.
    /// </summary>
    public static string Apply(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var cssLiteral = JsonSerializer.Serialize(css);
        var idLiteral = JsonSerializer.Serialize(StyleElementId);
        var hostLiteral = JsonSerializer.Serialize(Navigation.NavigationPolicy.WhatsAppHost);

        return $$"""
            (() => {
              if (window.top !== window || location.hostname !== {{hostLiteral}}) return;
              const css = {{cssLiteral}};
              const mount = () => {
                const parent = document.head || document.documentElement;
                if (!parent) return false;
                let style = document.getElementById({{idLiteral}});
                if (!style) {
                  style = document.createElement("style");
                  style.id = {{idLiteral}};
                  style.textContent = css;
                }
                parent.appendChild(style);
                return true;
              };
              if (!mount()) {
                new MutationObserver((_, observer) => { if (mount()) observer.disconnect(); })
                  .observe(document, { childList: true, subtree: true });
              }
              document.addEventListener("DOMContentLoaded", mount, { once: true });
            })();
            """;
    }
}
