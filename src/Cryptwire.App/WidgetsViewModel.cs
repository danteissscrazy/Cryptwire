using System.ComponentModel;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Widgets;

namespace Cryptwire.App;

/// <summary>
/// Lo que enseñan los widgets, igual en la barra que en el panel: el enlace (si hay red), Sigilo, Velo,
/// los mensajes sin leer y el reloj. Sigilo y Velo se guardan en tus ajustes a través de
/// <see cref="SkinController"/>; el resto se calcula al momento.
/// </summary>
internal sealed class WidgetsViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly SkinController skins;
    private readonly DispatcherTimer tick = new();
    private bool linkUp = NetworkInterface.GetIsNetworkAvailable();
    private int unread;

    public WidgetsViewModel(SkinController skins, Dispatcher dispatcher)
    {
        this.skins = skins;
        skins.StateChanged += (_, _) => Notify(string.Empty);

        // Un tic al empezar cada minuto: el reloj cambia a la vez que el de Windows y Sigilo descuenta.
        tick.Tick += (_, _) =>
        {
            ScheduleNextMinute();
            Notify(string.Empty);
            Ticked?.Invoke(this, EventArgs.Empty);
        };
        ScheduleNextMinute();
        tick.Start();

        // Windows avisa desde otro hilo: el cambio se pasa al de la ventana.
        NetworkChange.NetworkAvailabilityChanged += (_, e) => dispatcher.InvokeAsync(() =>
        {
            linkUp = e.IsAvailable;
            Notify(nameof(LinkUp));
            Notify(nameof(LinkLabel));
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Ha empezado un minuto: Sigilo puede haber acabado (la bandeja lo refleja).</summary>
    public event EventHandler? Ticked;

    public bool LinkUp => linkUp;

    public string LinkLabel => linkUp ? "enlace estable" : "sin red";

    /// <summary>Sigilo encendido: una hora sin tonos ni avisos. Pulsarlo de nuevo lo apaga.</summary>
    public bool StealthOn
    {
        get => Stealth.IsOn(skins.Settings.SilencedUntil, DateTimeOffset.Now);
        set
        {
            DateTimeOffset? until = value ? DateTimeOffset.Now + Stealth.Length : null;
            _ = skins.UpdateAsync(s => s with { SilencedUntil = until }, refresh: false);
        }
    }

    public string StealthLabel => StealthOn
        ? "Sin tonos ni avisos · " + Stealth.Label(skins.Settings.SilencedUntil, DateTimeOffset.Now)
        : "Una hora sin tonos ni avisos";

    public double StealthProgress => Stealth.Progress(skins.Settings.SilencedUntil, DateTimeOffset.Now);

    /// <summary>Velo encendido: los chats difuminados hasta pasar el ratón.</summary>
    public bool VeilOn
    {
        get => skins.Settings.Veil;
        set => _ = skins.UpdateAsync(s => s with { Veil = value });
    }

    public int Unread
    {
        get => unread;
        set
        {
            unread = value;
            Notify();
            Notify(nameof(UnreadLabel));
            Notify(nameof(HasUnread));
        }
    }

    public bool HasUnread => unread > 0;

    public string UnreadLabel => UnreadCount.Label(unread);

    public string Time => DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>«JUE 15 OCT», como en la maqueta.</summary>
    public string Date => DateTime.Now.ToString("ddd d MMM", Spanish).Replace(".", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private void ScheduleNextMinute()
    {
        var now = DateTime.Now;
        tick.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
