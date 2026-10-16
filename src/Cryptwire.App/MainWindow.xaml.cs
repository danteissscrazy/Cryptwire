using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Widgets;

namespace Cryptwire.App;

/// <summary>
/// La ventana: la barra de título propia (logo, nombre, piel activa, ajustes y botones), el navegador
/// embebido, que configura <see cref="WhatsAppHost"/>, y los widgets en barra o en panel. Da acceso a las
/// pieles y los widgets con el teclado (F7, F8, F9) y con el menú de la ventana (clic derecho en la barra
/// de título). Al cerrarla puede quedarse en la bandeja.
/// </summary>
public partial class MainWindow : Window
{
    private const string MaximizeGlyph = "M0.5,0.5 H9.5 V9.5 H0.5 Z";
    private const string RestoreGlyph = "M2.5,0.5 H9.5 V7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z";

    private const int WmMouseMove = 0x0200;
    private const int WmNcHitTest = 0x0084;
    private const int WmNcMouseMove = 0x00A0;
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmNcLButtonUp = 0x00A2;
    private const int WmNcMouseLeave = 0x02A2;
    private const int HtMaxButton = 9;
    private const uint TmeLeave = 0x2;
    private const uint TmeNonClient = 0x10;

    /// <summary>Espacio fino entre letras: WPF no tiene espaciado entre caracteres y la maqueta lo usa.</summary>
    private static readonly string HairSpace = char.ConvertFromUtf32(0x200A);

    private SkinController? skins;
    private SystemMenu? menu;
    private WindowState shownState = WindowState.Normal;
    private bool exiting;
    private bool closed;
    private bool startingHidden;
    private int unread = -1;

    public MainWindow()
    {
        InitializeComponent();
        SettingsLabel.Text = Spaced("ajustes");
        Closed += (_, _) => closed = true;
        StateChanged += (_, _) => FitToState();
        SizeChanged += (_, _) => ApplyWidgets();
        FitToState();
    }

    /// <summary>Has pedido los ajustes: botón de la barra o menú de la ventana.</summary>
    internal event EventHandler? SettingsRequested;

    /// <summary>
    /// Se pregunta al cerrar la ventana: true la esconde en la bandeja en vez de cerrar Cryptwire. No se
    /// pregunta al salir de verdad (<see cref="Exit"/>) ni cuando Windows se apaga.
    /// </summary>
    internal Func<bool>? HideOnClose { get; set; }

    /// <summary>La ventana se acaba de esconder en la bandeja.</summary>
    internal event EventHandler? HiddenToTray;

    /// <summary>Cerrada de verdad: no se puede volver a enseñar.</summary>
    internal bool IsClosed => closed;

    /// <summary>F9: el siguiente modo de widgets (barra, panel, ninguno).</summary>
    internal event EventHandler? WidgetModeRequested;

    /// <summary>Ctrl+W: cerrar el chat abierto.</summary>
    internal event EventHandler? CloseChatRequested;

    /// <summary>Los widgets enseñan lo que da este modelo, igual en la barra que en el panel.</summary>
    internal void ShowWidgets(WidgetsViewModel model)
    {
        WidgetBar.DataContext = model;
        WidgetPanel.DataContext = model;
        ApplyWidgets();
    }

    /// <summary>
    /// Conecta los atajos y el menú con las pieles. WebView2 pasa a la ventana las teclas de función
    /// antes que a la página, así que F7 y F8 funcionan aunque el foco esté en WhatsApp.
    /// </summary>
    internal void Bind(SkinController controller)
    {
        skins = controller;
        if (controller.StartWindow is not null)
        {
            // Donde la dejaste: sin centrarla encima de lo que aplica OnSourceInitialized.
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        Closing += OnClosing;
        menu = new SystemMenu(this);
        menu.SafeModeClicked += async (_, _) => await controller.ToggleSafeModeAsync();
        menu.ThemeClicked += async (_, id) => await controller.SelectAsync(id);
        menu.SettingsClicked += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        controller.StateChanged += (_, _) => ShowState();
        PreviewKeyDown += OnPreviewKeyDown;
        ShowState();
    }

    /// <summary>Trae Cryptwire delante: desde la bandeja, minimizada o detrás de otra ventana.</summary>
    internal void BringToFront()
    {
        if (closed)
        {
            // Cerrada de verdad: la app ya está terminando.
            return;
        }

        if (startingHidden)
        {
            // La has sacado de la bandeja antes de que acabara de cargar: se queda, como una ventana normal.
            startingHidden = false;
            ShowInTaskbar = true;
            ShowActivated = true;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = shownState;
        }

        Activate();
        WebView.Focus();
    }

    /// <summary>
    /// Arranque en la bandeja: WebView2 solo nace dentro de una ventana que se ha enseñado, así que se
    /// enseña minimizada, sin botón en la barra de tareas y sin quitarte el foco, hasta que WhatsApp está
    /// cargado (<see cref="FinishHiddenStart"/>).
    /// </summary>
    internal void StartHidden()
    {
        startingHidden = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        Show();
    }

    /// <summary>WhatsApp ya está cargado: la ventana pasa a la bandeja y vuelve a ser normal para la próxima vez.</summary>
    internal void FinishHiddenStart()
    {
        if (!startingHidden)
        {
            return;
        }

        Hide();
        startingHidden = false;
        ShowInTaskbar = true;
        ShowActivated = true;
    }

    /// <summary>Cierra de verdad: guarda dónde estaba la ventana y deja que la app termine.</summary>
    internal void Exit()
    {
        exiting = true;
        if (!closed)
        {
            Close();
        }
    }

    /// <summary>Los sin leer en el botón de la barra de tareas: un círculo del color de alerta con el número.</summary>
    internal void ShowUnread(int count)
    {
        if (count == unread)
        {
            return;
        }

        unread = count;
        Taskbar.Overlay = count > 0 ? Badge(UnreadCount.Label(count)) : null;
        Taskbar.Description = count > 0 ? $"Cryptwire · {UnreadCount.Label(count)} sin leer" : "Cryptwire";
    }

    /// <summary>La ventana ya existe para Windows pero aún no se ve: el momento de recolocarla.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (skins?.StartWindow is { } bounds)
        {
            WindowPlacement.Apply(this, bounds);
        }

        if (startingHidden)
        {
            // Se abrirá como la dejaste (maximizada o no) cuando la saques de la bandeja.
            shownState = WindowState;
            WindowState = WindowState.Minimized;
        }

        // Después de WindowChrome, para que este gancho vaya antes que el suyo.
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(OnMessage);
    }

    private static string Spaced(string text) => string.Join(HairSpace, text.ToUpperInvariant().EnumerateRunes());

    private ImageSource Badge(string label)
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var fill = (Brush)FindResource("Cw.Alert");
            var edge = new Pen((Brush)FindResource("Cw.Background"), 2);
            context.DrawEllipse(fill, edge, new Point(size / 2.0, size / 2.0), (size / 2.0) - 1, (size / 2.0) - 1);
            var text = new FormattedText(
                label.Length > 2 ? "99" : label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                label.Length > 1 ? 15 : 19,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting && HideOnClose?.Invoke() == true)
        {
            e.Cancel = true;
            skins?.RememberWindow(WindowPlacement.Read(this));
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Escondida en la bandeja, Windows ya no sabe si estaba maximizada: vale lo que se guardó al esconderla.
        if (IsVisible)
        {
            skins?.RememberWindow(WindowPlacement.Read(this));
        }
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (skins is null)
        {
            return;
        }

        // Ctrl+W: cerrar el chat abierto, como en WhatsApp para Windows.
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
        {
            e.Handled = true;
            CloseChatRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F7:
                e.Handled = true;
                await skins.NextThemeAsync();
                break;
            case Key.F8:
                e.Handled = true;
                await skins.ToggleSafeModeAsync();
                break;
            case Key.F9:
                e.Handled = true;
                WidgetModeRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>
    /// Barra, panel o nada, según tus ajustes y el ancho de la ventana (un panel en una ventana estrecha pasa
    /// a barra). En la barra de título, junto a «Ajustes», el modo elegido, como en la maqueta.
    /// </summary>
    private void ApplyWidgets()
    {
        if (skins is null || WidgetBar.DataContext is null)
        {
            return;
        }

        var settings = skins.Settings;
        var shown = WidgetLayout.Effective(settings.Widgets, ActualWidth, settings.PanelToBarWhenNarrow);
        WidgetBar.Visibility = shown == WidgetMode.Bar ? Visibility.Visible : Visibility.Collapsed;
        WidgetPanel.Visibility = shown == WidgetMode.Panel ? Visibility.Visible : Visibility.Collapsed;
        SettingsLabel.Text = Spaced("ajustes · " + WidgetLayout.Name(settings.Widgets));
    }

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnMinimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    /// <summary>Por si el clic llega por WPF (pantalla táctil, lector de pantalla) y no como botón de Windows.</summary>
    private void OnMaximize(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    /// <summary>
    /// El botón de maximizar se presenta a Windows como el suyo (<c>HTMAXBUTTON</c>): así Windows 11 enseña
    /// el panel de diseños de ajuste al pasar el ratón. A cambio, el ratón sobre él ya no llega a WPF: el
    /// estado (encima, pulsado) y el clic se llevan aquí.
    /// </summary>
    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmNcHitTest when OverMaximize(lParam):
                handled = true;
                return HtMaxButton;
            case WmNcMouseMove when wParam == HtMaxButton:
                if (MaximizeButton.Tag is not "pressed")
                {
                    MaximizeButton.Tag = "hover";
                }

                TrackLeave(hwnd);
                handled = true;
                break;
            case WmNcLButtonDown when wParam == HtMaxButton:
                MaximizeButton.Tag = "pressed";
                handled = true;
                break;
            case WmNcLButtonUp when wParam == HtMaxButton:
                var wasPressed = MaximizeButton.Tag is "pressed";
                MaximizeButton.Tag = "hover";
                if (wasPressed)
                {
                    ToggleMaximize();
                }

                handled = true;
                break;
            case WmNcMouseMove:
            case WmNcMouseLeave:
            case WmMouseMove:
                MaximizeButton.Tag = null;
                break;
        }

        return IntPtr.Zero;
    }

    private bool OverMaximize(IntPtr lParam)
    {
        if (!MaximizeButton.IsVisible || PresentationSource.FromVisual(MaximizeButton) is null)
        {
            return false;
        }

        var x = (short)(lParam.ToInt64() & 0xFFFF);
        var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        var point = MaximizeButton.PointFromScreen(new Point(x, y));
        return point.X >= 0 && point.Y >= 0 && point.X < MaximizeButton.ActualWidth && point.Y < MaximizeButton.ActualHeight;
    }

    private static void TrackLeave(IntPtr hwnd)
    {
        var track = new TrackMouseEventInfo { Size = (uint)Marshal.SizeOf<TrackMouseEventInfo>(), Flags = TmeLeave | TmeNonClient, Window = hwnd };
        TrackMouseEvent(ref track);
    }

    /// <summary>
    /// Maximizada, Windows estira la ventana por fuera de la pantalla lo que mide su borde invisible:
    /// se compensa para que la barra de título no quede cortada. El filo de la piel solo va con la ventana suelta.
    /// </summary>
    private void FitToState()
    {
        var maximized = WindowState == WindowState.Maximized;
        if (WindowState != WindowState.Minimized)
        {
            shownState = WindowState;
        }

        Frame.Margin = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
        Frame.BorderThickness = new Thickness(maximized ? 0 : 1);
        MaximizeIcon.Data = Geometry.Parse(maximized ? RestoreGlyph : MaximizeGlyph);
        MaximizeButton.ToolTip = maximized ? "Restaurar" : "Maximizar";
    }

    private void ShowState()
    {
        if (skins is null)
        {
            return;
        }

        Title = skins switch
        {
            { SafeMode: true } => "Cryptwire · modo seguro",
            { Active: { } theme } => $"Cryptwire · {theme.Name}",
            _ => "Cryptwire",
        };

        var tag = skins switch
        {
            { SafeMode: true } => "modo seguro",
            { Active: { } theme } => $"piel · {theme.Name}",
            _ => "sin piel",
        };
        SkinTag.Text = Spaced(tag);
        SkinTag.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, skins.SafeMode ? "Cw.Alert" : "Cw.Faint");
        menu?.Update(skins.Available, skins.Active, skins.SafeMode);
        ApplyWidgets();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackMouseEventInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr Window;
        public uint HoverTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TrackMouseEvent(ref TrackMouseEventInfo track);
}
