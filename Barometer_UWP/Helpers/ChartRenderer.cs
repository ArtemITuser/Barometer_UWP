using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Barometer_UWP.Helpers
{
    public struct ChartPoint { public double X; public double Y; }

    /// <summary>
    /// Draws a pressure polyline into any Canvas (used both by the interactive
    /// chart on GraphicsPage and by the live-tile mini chart UserControl).
    /// Pure shapes -> works on Windows 10 Mobile without extra packages.
    /// </summary>
    public static class ChartRenderer
    {
        public static void DrawPolyline(Canvas canvas, IList<ChartPoint> points,
            double width, double height, Color lineColor, double lineThickness = 2.0,
            bool fillUnder = true)
        {
            if (canvas == null || points == null || points.Count < 2 || width <= 0 || height <= 0) return;

            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            double spanX = Math.Max(maxX - minX, 1e-9);
            double spanY = Math.Max(maxY - minY, 1e-9);
            const double pad = 2;

            Func<ChartPoint, Point> project = p => new Point(
                pad + (p.X - minX) / spanX * (width - 2 * pad),
                height - pad - (p.Y - minY) / spanY * (height - 2 * pad));

            var coll = new PointCollection();
            foreach (var p in points) coll.Add(project(p));

            if (fillUnder)
            {
                var poly = new Polygon
                {
                    Points = new PointCollection(coll)
                };
                poly.Points.Add(new Point(width - pad, height));
                poly.Points.Add(new Point(pad, height));
                var fc = Color.FromArgb(60, lineColor.R, lineColor.G, lineColor.B);
                poly.Fill = new SolidColorBrush(fc);
                canvas.Children.Add(poly);
            }

            var line = new Polyline
            {
                Points = coll,
                Stroke = new SolidColorBrush(lineColor),
                StrokeThickness = lineThickness,
                StrokeLineJoin = PenLineRound
            };
            canvas.Children.Add(line);
        }

        /// <summary>Downsamples to at most maxPoints using min/max preserving buckets.</summary>
        public static List<ChartPoint> Decimate(IList<ChartPoint> src, int maxPoints)
        {
            if (src == null || src.Count <= maxPoints) return src?.ToList() ?? new List<ChartPoint>();
            var result = new List<ChartPoint>(maxPoints);
            double bucket = (double)src.Count / (maxPoints / 2);
            for (int i = 0; i < maxPoints / 2; i++)
            {
                int from = (int)Math.Floor(i * bucket);
                int to = Math.Min(src.Count - 1, (int)Math.Floor((i + 1) * bucket));
                if (to <= from) continue;
                var seg = src.Skip(from).Take(to - from + 1).ToList();
                var lo = seg.Aggregate((a, b) => a.Y <= b.Y ? a : b);
                var hi = seg.Aggregate((a, b) => a.Y >= b.Y ? a : b);
                if (lo.Y <= hi.Y) { result.Add(lo); result.Add(hi); }
                else { result.Add(hi); result.Add(lo); }
            }
            return result;
        }
    }
}

namespace Barometer_UWP.Helpers
{
    public static class ChartRendererBitmapExt
    {
    }
}
