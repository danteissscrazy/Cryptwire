using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>
/// Un aviso en la esquina de la pantalla. Nunca roba el foco: sale encima de lo que estés haciendo sin
/// quitarte el teclado. Dónde se coloca y cuándo se va lo decide <see cref="ToastPresenter"/>.
/// </summary>
internal sealed partial class ToastWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const double LobeRadius = 3.6;
    private const int Lobes = 12;

    /// <summary>Espacio fino entre letras: WPF no tiene espaciado entre caracteres y el diseño lo usa.</summary>
    private static readonly string HairSpace = char.ConvertFromUtf32(0x200A);

    private bool closing;

    public ToastWindow(Toast toast)
    {
        InitializeComponent();
        Toast = toast;
        Fill(toast);
        Root.MouseLeftButtonUp += (_, e) =>
        {
            if (closing || (e.OriginalSource is DependencyObject source && IsInside(source, CloseButton)))
            {
                return;
            }

            BodyClicked?.Invoke(this, EventArgs.Empty);
        };
        CloseButton.Click += (_, _) => CloseClicked?.Invoke(this, EventArgs.Empty);
        Loaded += (_, _) => Enter();
    }

    public event EventHandler? BodyClicked;

    public event EventHandler? CloseClicked;

    public Toast Toast { get; private set; }

    /// <summary>Otro aviso con la misma clave: se cambia el contenido sin cerrar ni volver a animar.</summary>
    public void Replace(Toast toast)
    {
        Toast = toast;
        Fill(toast);
    }

    /// <summary>Se va con un desvanecido corto y se cierra.</summary>
    public void Leave()
    {
        if (closing)
        {
            return;
        }

        closing = true;
        if (!Animated)
        {
            Close();
            return;
        }

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) => Close();
        Root.BeginAnimation(OpacityProperty, fade);
    }

    public void MoveTo(double left, double top)
    {
        Left = left;
        Top = top;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLongPtr(handle, GwlExStyle, (nint)(GetWindowLongPtr(handle, GwlExStyle) | WsExToolWindow | WsExNoActivate));
    }

    private static bool Animated => SystemParameters.ClientAreaAnimation;

    private static bool IsInside(DependencyObject element, DependencyObject container)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (current == container)
            {
                return true;
            }
        }

        return false;
    }

    private static string Spaced(string text) => string.Join(HairSpace, text.ToUpperInvariant().EnumerateRunes());

    private void Enter()
    {
        if (!Animated)
        {
            Root.Opacity = 1;
            Slide.X = 0;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }

    private void Fill(Toast toast)
    {
        var isCall = toast.Kind == NotificationKind.Call;
        var time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        var label = toast.Kind switch
        {
            NotificationKind.Call => "llamada entrante",
            NotificationKind.App => "cryptwire",
            _ => "mensaje",
        };
        KindLabel.Text = "◆ " + Spaced(label) + "  ·  " + time;
        KindLabel.SetResourceReference(TextBlock.ForegroundProperty, isCall ? "Cw.Alert" : "Cw.Accent");
        KindBar.SetResourceReference(Shape.FillProperty, isCall ? "Cw.Alert" : "Cw.Glow");
        Edge.SetResourceReference(Shape.FillProperty, isCall ? "Cw.Alert" : "Cw.AccentEdge");
        TitleText.Text = toast.Title;
        BodyText.Text = toast.Body;
        Title = $"Cryptwire · {toast.Title}";
        DrawAvatar(Initial(toast.Title), isCall);
    }

    /// <summary>La primera letra o emoji del nombre, sin los signos que WhatsApp pone delante de un teléfono.</summary>
    private static string Initial(string title)
    {
        var text = new StringInfo(title.Trim().TrimStart('+', '‪', '‫', '‎', '‏'));
        return text.LengthInTextElements > 0 ? text.SubstringByTextElements(0, 1).ToUpperInvariant() : "·";
    }

    /// <summary>
    /// El rosetón de la piel en pequeño: doce lóbulos alrededor del retrato, en oro (en carmesí si te
    /// llaman), con la inicial del chat dentro. No hace falta pedir la foto: así no sale nada a internet.
    /// </summary>
    private void DrawAvatar(string initial, bool isCall)
    {
        Avatar.Children.Clear();
        var lobeKey = isCall ? "Cw.Alert" : "Cw.Accent";
        const double center = 23;
        const double ring = 18.5;

        for (var i = 0; i < Lobes; i++)
        {
            var angle = i * Math.PI * 2 / Lobes;
            var lobe = new Ellipse { Width = LobeRadius * 2, Height = LobeRadius * 2, StrokeThickness = 1 };
            lobe.SetResourceReference(Shape.StrokeProperty, lobeKey);
            lobe.SetResourceReference(Shape.FillProperty, "Cw.Panel");
            Canvas.SetLeft(lobe, center + (ring * Math.Cos(angle)) - LobeRadius);
            Canvas.SetTop(lobe, center + (ring * Math.Sin(angle)) - LobeRadius);
            Avatar.Children.Add(lobe);
        }

        var disk = new Ellipse { Width = 34, Height = 34, StrokeThickness = 1.4 };
        disk.SetResourceReference(Shape.StrokeProperty, lobeKey);
        disk.SetResourceReference(Shape.FillProperty, "Cw.Background");
        Canvas.SetLeft(disk, center - 17);
        Canvas.SetTop(disk, center - 17);
        Avatar.Children.Add(disk);

        var letter = new TextBlock
        {
            Text = initial,
            Width = 34,
            TextAlignment = TextAlignment.Center,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)TitleText.FontFamily,
        };
        letter.SetResourceReference(TextBlock.ForegroundProperty, "Cw.Text");
        Canvas.SetLeft(letter, center - 17);
        Canvas.SetTop(letter, center - 11);
        Avatar.Children.Add(letter);

        if (isCall && Animated)
        {
            // Te están llamando: el rosetón late despacio.
            Avatar.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(700))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }
        else
        {
            Avatar.BeginAnimation(OpacityProperty, null);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr window, int index, nint value);
}
