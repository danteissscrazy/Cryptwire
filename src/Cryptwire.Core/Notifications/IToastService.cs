namespace Cryptwire.Core.Notifications;

public interface IToastService
{
    /// <summary>Enseña un aviso. Uno con la misma clave que otro que sigue a la vista lo sustituye.</summary>
    void Show(Toast toast);

    /// <summary>Quita el aviso de esa clave, si sigue a la vista. No avisa a nadie: lo ha cerrado la web.</summary>
    void Close(string key);
}

/// <summary>Un aviso para la pantalla, ya decidido: qué enseña y qué pasa al pulsarlo o descartarlo.</summary>
/// <param name="Key">Identifica el aviso mientras está a la vista.</param>
public sealed record Toast(string Key, NotificationKind Kind, string Title, string Body)
{
    /// <summary>Lo has pulsado: WhatsApp abre ese chat (o la llamada).</summary>
    public Action? Clicked { get; init; }

    /// <summary>Lo has cerrado tú, o se ha ido solo pasado su tiempo.</summary>
    public Action? Dismissed { get; init; }
}
