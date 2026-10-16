using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Cryptwire.App;

/// <summary>
/// La ventana: la barra de título propia (logo, nombre, piel activa y botones) y el navegador embebido,
/// que configura <see cref="WhatsAppHost"/>. Da acceso a las pieles con el teclado (F7, F8) y con el
/// menú de la ventana (clic derecho en la barra de título).
/// </summary>
public partial class MainWindow : Window
{
    private const string MaximizeGlyph = "M0.5,0.5 H9.5 V9.5 H0.5 Z";
    private const string RestoreGlyph = "M2.5,0.5 H9.5 V7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z";
    private static readonly Brush SafeModeBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x43, 0x5A));
    private static readonly Brush TagBrush = new SolidColorBrush(Color.FromRgb(0x67, 0x71, 0x89));

    /// <summary>Espacio fino entre letras: WPF no tiene espaciado entre caracteres y la maqueta lo usa.</summary>
    private static readonly string HairSpace = char.ConvertFromUtf32(0x200A);

    private SkinController? skins;
    private SystemMenu? menu;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => FitToState();
        FitToState();
    }

    /// <summary>
    /// Conecta los atajos y el menú con las pieles. WebView2 pasa a la ventana las teclas de función
    /// antes que a la página, así que F7 y F8 funcionan aunque el foco esté en WhatsApp.
    /// </summary>
    internal void Bind(SkinController controller)
    {
        skins = controller;
        menu = new SystemMenu(this);
        menu.SafeModeClicked += async (_, _) => await controller.ToggleSafeModeAsync();
        menu.ThemeClicked += async (_, id) => await controller.SelectAsync(id);
        controller.StateChanged += (_, _) => ShowState();
        PreviewKeyDown += OnPreviewKeyDown;
        ShowState();
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (skins is null || Keyboard.Modifiers != ModifierKeys.None)
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
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximize(object sender, RoutedEventArgs e)
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
    /// Maximizada, Windows estira la ventana por fuera de la pantalla lo que mide su borde invisible:
    /// se compensa para que la barra de título no quede cortada. El filo dorado solo va con la ventana suelta.
    /// </summary>
    private void FitToState()
    {
        var maximized = WindowState == WindowState.Maximized;
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
        SkinTag.Text = string.Join(HairSpace, tag.ToUpperInvariant().EnumerateRunes());
        SkinTag.Foreground = skins.SafeMode ? SafeModeBrush : TagBrush;
        menu?.Update(skins.Available, skins.Active, skins.SafeMode);
    }
}
