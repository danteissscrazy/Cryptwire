using System.Text.Json;
using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Layout;

/// <summary>
/// El JavaScript que deja ajustar el ancho de la lista de chats arrastrando la línea que la separa de
/// la conversación. WhatsApp Web no lo permite: su lista ocupa un porcentaje fijo de la ventana.
/// Recuerda un ancho para la ventana apaisada y otro para la vertical, y al girar el monitor usa el
/// suyo. Solo toca anchos: no lee mensajes ni nombres y no abre ningún canal hacia la app; el ancho se
/// guarda en el almacenamiento local de la propia página.
/// </summary>
public static class LayoutScript
{
    public const string StyleElementId = "cryptwire-layout";

    /// <summary>Clave en el <c>localStorage</c> de WhatsApp Web.</summary>
    public const string StorageKey = "cryptwire.panel";

    /// <summary>La lista nunca se estrecha más que esto…</summary>
    public const int MinListPixels = 260;

    /// <summary>…ni deja la conversación por debajo de esto.</summary>
    public const int MinChatPixels = 380;

    private static readonly string Guard =
        $"if (window.top !== window || location.hostname !== {JsonSerializer.Serialize(NavigationPolicy.WhatsAppHost)}) return;";

    private const string Slot = "window[Symbol.for(\"cryptwire-layout\")]";

    /// <summary>
    /// Instala el divisor en el documento. Sirve al crearse cada documento y sobre la página abierta;
    /// si ya está instalado, no hace nada.
    /// </summary>
    public static string Install() => $$"""
        (() => {
          {{Guard}}
          if ({{Slot}}) return;
          const KEY = {{JsonSerializer.Serialize(StorageKey)}};
          const MIN_LIST = {{MinListPixels}}, MIN_CHAT = {{MinChatPixels}}, RAIL = 64;
          const valid = r => typeof r === "number" && isFinite(r) && r >= 0.1 && r <= 0.8;
          const ratios = { landscape: null, portrait: null };
          try {
            const saved = JSON.parse(window.localStorage.getItem(KEY) || "{}");
            if (saved && valid(saved.landscape)) ratios.landscape = saved.landscape;
            if (saved && valid(saved.portrait)) ratios.portrait = saved.portrait;
          } catch (e) { }

          const style = document.createElement("style");
          style.id = {{JsonSerializer.Serialize(StyleElementId)}};
          const rule = r => "div:has(> #side) { flex: 0 0 clamp(" + MIN_LIST + "px, " + (r * 100).toFixed(2) +
            "%, calc(100% - " + (MIN_CHAT + RAIL) + "px)) !important; max-width: none !important; min-width: 0 !important; }";
          const render = () => {
            let css =
              "html.cw-panel-edge, html.cw-panel-edge *, html.cw-panel-drag, html.cw-panel-drag * { cursor: col-resize !important; }" +
              "html.cw-panel-drag * { user-select: none !important; }" +
              "html:is(.cw-panel-edge, .cw-panel-drag) div:has(> #side) { box-shadow: inset -2px 0 0 var(--cw-ice-400, #53bdeb) !important; }";
            if (ratios.landscape !== null) css += "@media (orientation: landscape) { " + rule(ratios.landscape) + " }";
            if (ratios.portrait !== null) css += "@media (orientation: portrait) { " + rule(ratios.portrait) + " }";
            style.textContent = css;
          };
          const save = () => { try { window.localStorage.setItem(KEY, JSON.stringify(ratios)); } catch (e) { } };
          const orientation = () => (window.innerHeight > window.innerWidth ? "portrait" : "landscape");
          const html = () => document.documentElement;
          const column = () => { const side = document.getElementById("side"); return side ? side.parentElement : null; };

          // Cerca de la línea: 3 px dentro de la lista (su barra de scroll queda libre) o 6 px fuera,
          // y sobre la propia pantalla de chats, nunca sobre un menú o una ventana de la web encima.
          const near = e => {
            const col = column();
            if (!col || !col.parentElement || !col.parentElement.contains(e.target)) return false;
            const r = col.getBoundingClientRect();
            const dx = e.clientX - r.right;
            return e.clientY >= r.top && e.clientY <= r.bottom && dx >= -3 && dx <= 6;
          };

          let drag = null, hover = false;
          const setHover = on => {
            if (on === hover || !html()) return;
            hover = on;
            html().classList.toggle("cw-panel-edge", on);
          };
          const onMove = e => {
            if (!drag) { setHover(near(e)); return; }
            const min = Math.max(0.1, MIN_LIST / drag.width);
            const max = Math.min(0.8, (drag.width - drag.offset - MIN_CHAT) / drag.width);
            const ratio = (e.clientX - drag.left) / drag.width;
            ratios[drag.orientation] = Math.min(Math.max(ratio, min), Math.max(min, max));
            render();
            e.preventDefault();
          };
          const onDown = e => {
            if (e.button !== 0 || !near(e)) return;
            const col = column();
            const box = col.parentElement.getBoundingClientRect();
            const left = col.getBoundingClientRect().left;
            drag = { left, offset: left - box.left, width: box.width, orientation: orientation() };
            html().classList.add("cw-panel-drag");
            e.preventDefault();
            e.stopPropagation();
          };
          const onUp = () => {
            if (!drag) return;
            drag = null;
            html().classList.remove("cw-panel-drag");
            save();
          };
          // Doble clic en la línea: vuelve al ancho de WhatsApp para esta orientación.
          const onDouble = e => {
            if (!near(e)) return;
            ratios[orientation()] = null;
            render();
            save();
            e.preventDefault();
            e.stopPropagation();
          };

          const listeners = [["pointermove", onMove, true], ["pointerdown", onDown, true], ["pointerup", onUp, true], ["blur", onUp, false], ["dblclick", onDouble, true]];
          for (const [type, fn, capture] of listeners) window.addEventListener(type, fn, capture);

          render();
          const mount = () => {
            const parent = document.head || document.documentElement;
            if (!parent) return false;
            parent.appendChild(style);
            return true;
          };
          if (!mount()) {
            new MutationObserver((_, observer) => { if (mount()) observer.disconnect(); })
              .observe(document, { childList: true, subtree: true });
          }

          {{Slot}} = {
            remove: () => {
              for (const [type, fn, capture] of listeners) window.removeEventListener(type, fn, capture);
              style.remove();
              if (html()) html().classList.remove("cw-panel-edge", "cw-panel-drag");
              {{Slot}} = undefined;
            },
          };
        })();
        """;

    /// <summary>Quita el divisor de la página abierta (modo seguro): vuelve el ancho de WhatsApp.</summary>
    public static string Remove() => $$"""
        (() => {
          {{Guard}}
          const installed = {{Slot}};
          if (installed) installed.remove();
        })();
        """;
}
