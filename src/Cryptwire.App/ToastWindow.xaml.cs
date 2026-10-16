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
    private const int Blades = 12;
    private const double PhotoSize = 31;

    /// <summary>Espacio fino entre letras: WPF no tiene espaciado entre caracteres y el diseño lo usa.</summary>
    private static readonly string HairSpace = char.ConvertFromUtf32(0x200A);

    private bool closing;
    private Ellipse? photo;

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
        _ = LoadPhotoAsync(toast);
    }

    /// <summary>La foto llega después del aviso: así nunca lo retrasa, y si no llega se queda la inicial.</summary>
    private async Task LoadPhotoAsync(Toast toast)
    {
        var image = await AvatarLoader.LoadAsync(toast.IconUri);
        if (image is null || closing || !ReferenceEquals(Toast, toast) || photo is null)
        {
            return;
        }

        photo.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
    }

    /// <summary>La primera letra o emoji del nombre, sin los signos que WhatsApp pone delante de un teléfono.</summary>
    private static string Initial(string title)
    {
        var text = new StringInfo(title.Trim().TrimStart('+', '\u202A', '\u202B', '\u200E', '\u200F'));
        return text.LengthInTextElements > 0 ? text.SubstringByTextElements(0, 1).ToUpperInvariant() : "·";
    }

    /// <summary>
    /// El marco de los avatares de la piel en pequeño: doce cuchillas en espiral, como una sierra, en oro
    /// (en carmesí si te llaman, y entonces giran), con la foto del chat dentro o, mientras llega o si no
    /// hay, su inicial. Es el mismo dibujo que <c>tools/generar-cuchillas.py</c>.
    /// </summary>
    private void DrawAvatar(string initial, bool isCall)
    {
        Avatar.Children.Clear();
        var key = isCall ? "Cw.Alert" : "Cw.Accent";
        const double center = 23;

        var blades = new Path { Data = Geometry.Parse(BladesGeometry()), RenderTransform = new RotateTransform(0, center, center) };
        blades.SetResourceReference(Shape.FillProperty, key);
        Avatar.Children.Add(blades);

        var disk = new Ellipse { Width = 34, Height = 34, StrokeThickness = 1.8 };
        disk.SetResourceReference(Shape.StrokeProperty, key);
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

        // La foto del chat tapa la inicial cuando llega (LoadPhotoAsync); hasta entonces, transparente.
        photo = new Ellipse { Width = PhotoSize, Height = PhotoSize };
        Canvas.SetLeft(photo, center - (PhotoSize / 2));
        Canvas.SetTop(photo, center - (PhotoSize / 2));
        Avatar.Children.Add(photo);

        var edge = new Ellipse { Width = PhotoSize, Height = PhotoSize, StrokeThickness = 0.8, Opacity = 0.45 };
        edge.SetResourceReference(Shape.StrokeProperty, "Cw.Glow");
        Canvas.SetLeft(edge, center - (PhotoSize / 2));
        Canvas.SetTop(edge, center - (PhotoSize / 2));
        Avatar.Children.Add(edge);

        if (isCall && Animated)
        {
            // Te están llamando: la sierra gira.
            blades.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(3))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }
    }

    /// <summary>
    /// Doce cuchillas alrededor de un aro de radio 17 en un cuadro de 46: largas y cortas alternas, con la
    /// punta adelantada respecto a la base para que parezcan girar.
    /// </summary>
    private static string BladesGeometry()
    {
        const double center = 23;
        const double baseRadius = 16.4;
        var path = new System.Text.StringBuilder();
        for (var i = 0; i < Blades; i++)
        {
            var angle = (i * 360.0 / Blades) - 90;
            var tip = i % 2 == 0 ? 23 : 21;
            path.Append(CultureInfo.InvariantCulture, $"M{Point(baseRadius, angle - 13)} L{Point(tip, angle + 9)} L{Point(baseRadius, angle + 5)} Z ");
        }

        return path.ToString();

        static string Point(double radius, double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return string.Create(CultureInfo.InvariantCulture, $"{center + (radius * Math.Cos(radians)):0.##},{center + (radius * Math.Sin(radians)):0.##}");
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr window, int index, nint value);
}
