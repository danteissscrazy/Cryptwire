using System.Windows.Controls;

namespace Cryptwire.App;

/// <summary>La barra de widgets bajo la conversación. Sin lógica: se enlaza a <see cref="WidgetsViewModel"/>.</summary>
internal sealed partial class WidgetBar : UserControl
{
    public WidgetBar() => InitializeComponent();
}
