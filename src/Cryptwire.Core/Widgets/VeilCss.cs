namespace Cryptwire.Core.Widgets;

/// <summary>
/// Velo: difumina los chats (nombres, últimos mensajes y fotos de la lista, y los mensajes y la cabecera de
/// la conversación) hasta que pasas el ratón por encima. Para tener WhatsApp abierto con gente alrededor.
/// Es CSS en la página, como la piel: no lee ni toca nada.
/// </summary>
public static class VeilCss
{
    public const string Css = """

        /* Cryptwire · Velo */
        #pane-side [data-testid="cell-frame-container"] :is([data-testid="cell-frame-title"], [data-testid="cell-frame-secondary"], img),
        #main header :is(img, [data-testid="conversation-info-header"]),
        #main [data-testid="conversation-panel-messages"] [data-testid="msg-container"] {
          filter: blur(7px) !important;
          transition: filter .18s ease;
        }

        #pane-side [data-testid="cell-frame-container"]:hover :is([data-testid="cell-frame-title"], [data-testid="cell-frame-secondary"], img),
        #main header:hover :is(img, [data-testid="conversation-info-header"]),
        #main [data-testid="conversation-panel-messages"] [data-testid="msg-container"]:hover {
          filter: none !important;
        }

        """;
}

/// <summary>Todo el CSS que Cryptwire pone en la página, en orden: la piel, tu fondo y el velo.</summary>
public static class PageCss
{
    /// <returns>El CSS junto, o null si no hay nada que poner (WhatsApp tal cual).</returns>
    public static string? Compose(string? skin, string? wallpaper, bool veil)
    {
        var css = (skin ?? string.Empty) + (wallpaper ?? string.Empty) + (veil ? VeilCss.Css : string.Empty);
        return css.Length > 0 ? css : null;
    }
}
