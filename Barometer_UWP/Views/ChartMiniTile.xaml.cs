using System;
using System.Collections.Generic;
using Barometer_UWP.Helpers;
using Windows.UI;
using Windows.UI.Xaml.Controls;

namespace Barometer_UWP.Views
{
    /// <summary>
    /// Lightweight mini pressure chart used on the wide live tile and inside pages.
    /// Renders with pure XAML shapes (Canvas + Polyline) — no external charting
    /// packages, so it works on Windows 10 Mobile ARM devices.
    /// </summary>
    public sealed partial class ChartMiniTile : UserControl
    {
        private List<ChartPoint> _points = new List<ChartPoint>();
        private Color _lineColor = Color.FromArgb(255, 0, 160, 255);

        public ChartMiniTile()
        {
            this.InitializeComponent();
        }

        public void SetData(IEnumerable<double> values, string currentText, string unitText)
        {
            _points.Clear();
            if (values != null)
            {
                int i = 0;
                foreach (var v in values)
                {
                    _points.Add(new ChartPoint { X = i++, Y = v });
                }
            }
            ValueText.Text = currentText ?? "--";
            UnitText.Text = unitText ?? string.Empty;
            Redraw();
        }

        public void SetLineColor(Color c)
        {
            _lineColor = c;
            Redraw();
        }

        private void ChartCanvas_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            if (ChartCanvas == null) return;
            ChartCanvas.Children.Clear();
            var w = ChartCanvas.ActualWidth;
            var h = ChartCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var decimated = ChartRenderer.Decimate(_points, Math.Max(8, (int)w));
            ChartRenderer.DrawPolyline(ChartCanvas, decimated, w, h, _lineColor, 2.0, true);
        }
    }
}
