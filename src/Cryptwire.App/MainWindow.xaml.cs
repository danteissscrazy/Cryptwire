using System.Windows;
using System.Windows.Input;

namespace Cryptwire.App;

/// <summary>
/// La ventana: contiene el navegador embebido, que configura <see cref="WhatsAppHost"/>, y da acceso a
/// las pieles con el teclado (F7, F8) y con el menú de la ventana.
/// </summary>
public partial class MainWindow : Window
{
    private SkinController? skins;
    private SystemMenu? menu;

    public MainWindow()
    {
        InitializeComponent();
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
        menu?.Update(skins.Available, skins.Active, skins.SafeMode);
    }
}
