using System.Globalization;
using System.Windows.Data;

namespace Cryptwire.App;

/// <summary>
/// «on» si el valor enlazado es igual al parámetro; si no, nada. Marca cuál de los botones de un grupo
/// está elegido (piel, ninguno, archivo) sin que el botón guarde su propio estado.
/// </summary>
internal sealed class OnIfConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal) ? "on" : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
