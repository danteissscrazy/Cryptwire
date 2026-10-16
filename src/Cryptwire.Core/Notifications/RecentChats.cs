namespace Cryptwire.Core.Notifications;

/// <summary>
/// Los últimos chats que te han avisado, para elegirlos en la pantalla de tonos sin escribir su nombre.
/// Solo en memoria y solo el nombre: se pierde al cerrar la app y nunca guarda qué decía el aviso.
/// </summary>
public sealed class RecentChats
{
    public const int Capacity = 15;

    private readonly List<string> names = [];

    /// <summary>Del más reciente al más antiguo, sin repetidos.</summary>
    public IReadOnlyList<string> Names => names;

    public void Add(string? title)
    {
        if (ChatName.Normalize(title) is not { } name)
        {
            return;
        }

        names.RemoveAll(n => ChatName.Same(n, name));
        names.Insert(0, name);
        if (names.Count > Capacity)
        {
            names.RemoveRange(Capacity, names.Count - Capacity);
        }
    }
}
