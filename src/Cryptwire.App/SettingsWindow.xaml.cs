using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Cryptwire.App;

/// <summary>
/// La pantalla «Ajustes de Cryptwire». Sin lógica: todo está en <see cref="SettingsViewModel"/>. Se cierra
/// con Esc o con la cruz; lo que cambias ya está guardado.
/// </summary>
internal sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel model;

    public SettingsWindow(SettingsViewModel model)
    {
        InitializeComponent();
        this.model = model;
        DataContext = model;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        Closed += (_, _) => model.Detach();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

/// <summary>Visible cuando el valor es false: el texto de «aún no hay» cuando la lista está vacía.</summary>
internal sealed class InverseVisibilityConverter : IValueConverter
{
    public static InverseVisibilityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Un porcentaje (0-100) como opacidad (0-1): la vista previa del fondo se oscurece como en la página.</summary>
internal sealed class PercentConverter : IValueConverter
{
    public static PercentConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int percent ? Math.Clamp(percent, 0, 100) / 100.0 : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
