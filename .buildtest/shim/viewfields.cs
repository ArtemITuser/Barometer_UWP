
namespace Barometer_UWP.Views {
  public partial class ChartMiniTile { protected global::Windows.UI.Xaml.Controls.TextBlock ValueBlock; protected global::Windows.UI.Xaml.Controls.TextBlock UnitBlock; protected global::Windows.UI.Xaml.Controls.Canvas ChartCanvas; private global::Windows.UI.Xaml.Controls.Canvas canvas => ChartCanvas; }
  public partial class GraphicsPage { protected global::Windows.UI.Xaml.Controls.Grid ChartHost; protected global::Windows.UI.Xaml.Controls.TextBlock TooltipText; protected TooltipBorderImpl TooltipBorder; }
  // harness-only: Border with Visibility (real UWP FrameworkElement.Visibility is shimmed here for the generated field)
  public class TooltipBorderImpl : Windows.UI.Xaml.Controls.Border { public new Windows.UI.Xaml.Visibility Visibility { get; set; } }
}
