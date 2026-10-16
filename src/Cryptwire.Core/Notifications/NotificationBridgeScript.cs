using System.Text.Json;
using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Notifications;

/// <summary>
/// El JavaScript que lleva a Cryptwire los avisos que WhatsApp lanza desde su service worker.
/// </summary>
/// <remarks>
/// WebView2 solo deja interceptar los avisos normales (<c>new Notification</c>). Los que la web lanza con
/// <c>registration.showNotification</c> (las llamadas y el aviso de prueba, los que llevan botones) los
/// pinta WebView2 como un aviso de Windows de los de siempre, sin pasar por Cryptwire: suena el tono de
/// Windows y sale un segundo aviso. Este script cambia, solo en la página de WhatsApp, esa llamada por un
/// aviso normal: así llega a Cryptwire como los demás, marcado en su <c>tag</c> como llamada o como aviso
/// del service worker. WhatsApp sigue pudiendo cerrarlo: <c>getNotifications</c> también devuelve estos.
/// No lee mensajes ni abre ningún canal hacia la app; si algo falla, deja pasar la llamada original.
/// </remarks>
public static class NotificationBridgeScript
{
    /// <summary>Prefijo del <c>tag</c> de un aviso de llamada (WhatsApp lo marca con <c>data.callId</c>).</summary>
    public const string CallTagPrefix = "cryptwire:llamada:";

    /// <summary>Prefijo del <c>tag</c> de cualquier otro aviso que venía por el service worker.</summary>
    public const string WorkerTagPrefix = "cryptwire:aviso:";

    private static readonly string Guard =
        $"if (window.top !== window || location.hostname !== {JsonSerializer.Serialize(NavigationPolicy.WhatsAppHost)}) return;";

    /// <summary>Guarda las funciones originales: sirve para no instalarlo dos veces y para quitarlo.</summary>
    private const string Slot = "window[Symbol.for(\"cryptwire-avisos\")]";

    public static bool IsCallTag(string? tag) => tag?.StartsWith(CallTagPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Instala el puente en el documento. Se ejecuta al crearse cada documento (antes que el código de
    /// WhatsApp) y sobre la página ya abierta; si ya está instalado, no hace nada.
    /// </summary>
    public static string Install() => $$"""
        (() => {
          {{Guard}}
          if ({{Slot}}) return;
          const Registration = window.ServiceWorkerRegistration;
          const Native = window.Notification;
          if (!Registration || !Registration.prototype || typeof Native !== "function") return;
          const proto = Registration.prototype;
          const original = { show: proto.showNotification, get: proto.getNotifications };
          if (typeof original.show !== "function") return;
          const open = [];

          proto.showNotification = function (title, options) {
            const o = options || {};
            const data = o.data == null ? null : o.data;
            const isCall = data !== null && typeof data === "object" && data.callId != null;
            const tag = o.tag == null ? "" : String(o.tag);
            let shown;
            try {
              shown = new Native(title == null ? "" : String(title), {
                body: o.body == null ? "" : String(o.body),
                tag: (isCall ? {{JsonSerializer.Serialize(CallTagPrefix)}} : {{JsonSerializer.Serialize(WorkerTagPrefix)}}) + tag,
                icon: o.icon,
                lang: o.lang,
                dir: o.dir,
                silent: true,
                requireInteraction: o.requireInteraction === true,
                data: data
              });
            } catch (e) {
              return original.show.apply(this, arguments);
            }

            // Se olvida al cerrarlo la página (WebView2 no le devuelve el evento «close») o al cerrarlo tú.
            let entry = null;
            const forget = function () {
              const at = open.indexOf(entry);
              if (at >= 0) open.splice(at, 1);
            };
            entry = {
              title: shown.title,
              body: shown.body,
              tag: tag,
              data: data,
              close: function () { forget(); shown.close(); }
            };
            open.push(entry);
            shown.addEventListener("close", forget);
            return Promise.resolve();
          };

          proto.getNotifications = function (filter) {
            const wanted = filter && filter.tag != null ? String(filter.tag) : null;
            const mine = open.filter(function (e) { return wanted === null || e.tag === wanted; });
            let theirs;
            try {
              theirs = typeof original.get === "function" ? original.get.apply(this, arguments) : [];
            } catch (e) {
              theirs = [];
            }
            return Promise.resolve(theirs).then(
              function (list) { return Array.prototype.slice.call(list || []).concat(mine); },
              function () { return mine; });
          };

          {{Slot}} = { proto: proto, show: original.show, get: original.get };
        })();
        """;

    /// <summary>Quita el puente de la página abierta (modo seguro): vuelven las funciones originales.</summary>
    public static string Remove() => $$"""
        (() => {
          {{Guard}}
          const saved = {{Slot}};
          if (!saved) return;
          saved.proto.showNotification = saved.show;
          if (typeof saved.get === "function") {
            saved.proto.getNotifications = saved.get;
          } else {
            delete saved.proto.getNotifications;
          }
          {{Slot}} = null;
        })();
        """;
}
