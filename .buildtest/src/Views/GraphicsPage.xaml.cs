using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Barometer_UWP.ViewModels;

namespace Barometer_UWP.Views
{
    public sealed partial class GraphicsPage : Page
    {
        public GraphicsViewModel ViewModel { get; private set; }

        private double _panAccumPx;
        private double _zoomAccum = 1.0;

        public GraphicsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel = new GraphicsViewModel();
            DataContext = ViewModel;
            _panAccumPx = 0; _zoomAccum = 1.0;
            _ = ViewModel.ReloadAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel?.Dispose();
            ViewModel = null;
            base.OnNavigatedFrom(e);
        }

        // ---- gestures ---------------------------------------------------------

        private void ChartHost_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _panAccumPx = 0;
            _zoomAccum = 1.0;
            HideTooltip();
            e.Handled = true;
        }

        private void ChartHost_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Horizontal drag => pan through time.
            _panAccumPx += e.Delta.Translation.X;
            double plotWidth = Math.Max(50, ChartHost.ActualWidth);
            int shift = -(int)(_panAccumPx / (plotWidth / 8)); // every 1/8 width = one step
            if (shift != 0)
            {
                _panAccumPx = 0;
                vm.PanBy(shift);
            }

            // Pinch => zoom the value axis around its center.
            double scale = e.Delta.Scale;
            if (Math.Abs(scale - 1.0) > 0.001)
            {
                _zoomAccum *= scale;
                if (_zoomAccum > 1.06) { vm.ZoomY(0.9); _zoomAccum = 1.0; }
                else if (_zoomAccum < 0.94) { vm.ZoomY(1.1); _zoomAccum = 1.0; }
            }
            e.Handled = true;
        }

        private void ChartHost_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || !vm.HasData) { HideTooltip(); return; }

            Point p = e.GetPosition(ChartHost);
            var hit = vm.HitTest(p.X / Math.Max(1, ChartHost.ActualWidth),
                                 p.Y / Math.Max(1, ChartHost.ActualHeight));
            if (hit == null) { HideTooltip(); return; }

            TooltipText.Text = hit;
            Canvas.SetLeft(TooltipBorder, Math.Min(p.X + 10, ChartHost.ActualWidth - 170));
            Canvas.SetTop(TooltipBorder, Math.Max(0, p.Y - 40));
            TooltipBorder.Visibility = Visibility.Visible;
        }

        private void HideTooltip() => TooltipBorder.Visibility = Visibility.Collapsed;
    }
}
