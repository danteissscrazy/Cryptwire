using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Cryptwire.App;

/// <summary>
/// Etiquetas en mayúsculas con las letras separadas, como en la maqueta («T O N O S»). WPF no tiene
/// espaciado entre letras: se mete un espacio finísimo entre cada una. En XAML:
/// <c>&lt;TextBlock app:Letterspace.Text="tonos" /&gt;</c>.
/// </summary>
internal static class Letterspace
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Letterspace), new PropertyMetadata(null, OnTextChanged));

    private static readonly string HairSpace = char.ConvertFromUtf32(0x200A);

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static string Spaced(string? text) =>
        string.Join(HairSpace, (text ?? string.Empty).ToUpperInvariant().EnumerateRunes());

    /// <summary>Se ve espaciado, pero un lector de pantalla lee la palabra tal cual, no letra a letra.</summary>
    private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is TextBlock block)
        {
            block.Text = Spaced(e.NewValue as string);
            AutomationProperties.SetName(block, e.NewValue as string ?? string.Empty);
        }
    }
}
