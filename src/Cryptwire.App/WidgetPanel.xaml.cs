using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Cryptwire.App;

/// <summary>El panel de widgets a la derecha. Sin lógica: se enlaza a <see cref="WidgetsViewModel"/>.</summary>
internal sealed partial class WidgetPanel : UserControl
{
    public WidgetPanel() => InitializeComponent();
}

/// <summary>Una fracción (0-1) por un ancho: cuánto de la barrita de Sigilo queda en oro.</summary>
internal sealed class ScaleConverter : IValueConverter
{
    public static ScaleConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double fraction && double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            ? Math.Clamp(fraction, 0, 1) * width
            : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
