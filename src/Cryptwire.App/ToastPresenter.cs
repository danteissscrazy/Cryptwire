using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>
/// Los avisos de Cryptwire en la esquina de abajo a la derecha, apilados hacia arriba en la pantalla
/// donde está la ventana. Un mensaje se va solo a los pocos segundos (no mientras el ratón está encima);
/// una llamada se queda mientras suena. Pulsar un aviso trae Cryptwire delante y abre ese chat.
/// </summary>
internal sealed class ToastPresenter(Window main, Action bringToFront) : IToastService
{
    private const int MaxVisible = 4;
    private const double Margin = 14;
    private const double Gap = 10;
    private static readonly TimeSpan MessageLife = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan AfterHover = TimeSpan.FromSeconds(3);

    private readonly List<Entry> open = [];

    public void Show(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);

        var existing = open.Find(e => e.Window.Toast.Key == toast.Key);
        if (existing is not null)
        {
            existing.Window.Replace(toast);
            existing.Restart(Life(toast));
            return;
        }

        // Sin sitio, se va el aviso más antiguo, pero nunca una llamada que sigue sonando.
        while (open.Count >= MaxVisible && open.Find(e => e.Window.Toast.Kind != NotificationKind.Call) is { } oldest)
        {
            Dismiss(oldest);
        }

        var window = new ToastWindow(toast);
        var entry = new Entry(window);
        entry.Timer.Tick += (_, _) => Dismiss(entry);
        window.BodyClicked += (_, _) => Click(entry);
        window.CloseClicked += (_, _) => Dismiss(entry);
        window.MouseEnter += (_, _) => entry.Timer.Stop();
        window.MouseLeave += (_, _) =>
        {
            // Uno que ya se está yendo no vuelve a contar tiempo.
            if (!entry.Gone)
            {
                entry.Restart(entry.Window.Toast.Kind == NotificationKind.Call ? Life(entry.Window.Toast) : AfterHover);
            }
        };
        open.Add(entry);

        Layout();
        window.Show();
        entry.Restart(Life(toast));
    }

    public void Close(string key)
    {
        if (open.Find(e => e.Window.Toast.Key == key) is { } entry)
        {
            Remove(entry);
        }
    }

    private static TimeSpan Life(Toast toast) => toast.Kind == NotificationKind.Call ? NotificationRouter.MaxRing : MessageLife;

    private void Click(Entry entry)
    {
        if (Remove(entry))
        {
            bringToFront();
            entry.Window.Toast.Clicked?.Invoke();
        }
    }

    private void Dismiss(Entry entry)
    {
        if (Remove(entry))
        {
            entry.Window.Toast.Dismissed?.Invoke();
        }
    }

    /// <returns>True si seguía a la vista: así un aviso que ya se fue no avisa dos veces a la página.</returns>
    private bool Remove(Entry entry)
    {
        entry.Timer.Stop();
        if (entry.Gone)
        {
            return false;
        }

        entry.Gone = true;
        open.Remove(entry);
        entry.Window.Leave();
        Layout();
        return true;
    }

    /// <summary>El más reciente abajo del todo; los anteriores suben.</summary>
    private void Layout()
    {
        var area = WorkArea();
        var bottom = area.Bottom - Margin;
        for (var i = open.Count - 1; i >= 0; i--)
        {
            var window = open[i].Window;
            bottom -= window.Height;
            window.MoveTo(area.Right - Margin - window.Width, bottom);
            bottom -= Gap;
        }
    }

    /// <summary>
    /// La zona útil (sin la barra de tareas) del monitor donde está Cryptwire, en las unidades de WPF. Con
    /// la ventana escondida en la bandeja, la del monitor principal.
    /// </summary>
    private Rect WorkArea()
    {
        var handle = new WindowInteropHelper(main).Handle;
        if (handle == IntPtr.Zero || !main.IsVisible || PresentationSource.FromVisual(main) is not { CompositionTarget: { } target })
        {
            return SystemParameters.WorkArea;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info))
        {
            return SystemParameters.WorkArea;
        }

        var fromDevice = target.TransformFromDevice;
        var topLeft = fromDevice.Transform(new Point(info.Work.Left, info.Work.Top));
        var bottomRight = fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private sealed class Entry(ToastWindow window)
    {
        public ToastWindow Window { get; } = window;

        public DispatcherTimer Timer { get; } = new();

        /// <summary>Ya no está a la vista (pulsado, cerrado o sustituido): se ignora todo lo que llegue tarde.</summary>
        public bool Gone { get; set; }

        public void Restart(TimeSpan after)
        {
            Timer.Stop();
            Timer.Interval = after;
            Timer.Start();
        }
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
