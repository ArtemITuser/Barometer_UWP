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

        /// <summary>
        /// Projects chart points onto a WriteableBitmap and draws the polyline plus a
        /// translucent area fill directly into pixel space (no XAML shapes involved).
        /// Used by GraphicsViewModel for chart export (PNG/JPEG) and tile rendering.
        /// </summary>
        public static void DrawPolylineOnBitmap(WriteableBitmap bmp, IList<ChartPoint> points,
            Color lineColor, double lineThickness = 2.0, bool fillUnder = true)
        {
            if (bmp == null || points == null || points.Count < 2) return;

            double width = bmp.PixelWidth, height = bmp.PixelHeight;
            if (width <= 0 || height <= 0) return;

            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            double spanX = Math.Max(maxX - minX, 1e-9);
            double spanY = Math.Max(maxY - minY, 1e-9);
            const double pad = 4;

            Func<ChartPoint, Point> project = p => new Point(
                pad + (p.X - minX) / spanX * (width - 2 * pad),
                height - pad - (p.Y - minY) / spanY * (height - 2 * pad));

            var projected = points.Select(project).ToList();

            int stride = (int)width * 4;
            byte[] px = new byte[stride * (int)height];

            if (fillUnder)
            {
                // column-wise area fill between the line and the bottom edge
                byte fb = lineColor.B, fg = lineColor.G, fr = lineColor.R;
                byte fa = (byte)Math.Min(255, (int)(lineColor.A * 0.25));
                for (int i = 0; i < projected.Count - 1; i++)
                {
                    Point a = projected[i], b = projected[i + 1];
                    int x0 = (int)Math.Floor(Math.Min(a.X, b.X));
                    int x1 = (int)Math.Ceiling(Math.Max(a.X, b.X));
                    for (int x = Math.Max(0, x0); x <= Math.Min((int)width - 1, x1); x++)
                    {
                        double f = (b.X - a.X) == 0 ? 0 : (x - a.X) / (b.X - a.X);
                        double yTop = a.Y + (b.Y - a.Y) * f;
                        int yt = Math.Max(0, (int)Math.Round(yTop));
                        for (int y = yt; y < (int)height; y++)
                            BlendInto(px, y * stride + x * 4, fb, fg, fr, fa);
                    }
                }
            }

            int t = Math.Max(1, (int)Math.Round(lineThickness));
            int half = t / 2;
            for (int i = 0; i < projected.Count - 1; i++)
                DrawSegmentInto(px, stride, (int)width, (int)height,
                    projected[i], projected[i + 1], lineColor, half);

            using (var stream = bmp.OpenStream())
            {
                stream.Position = 0;
                stream.Write(px, 0, px.Length);
                stream.Flush();
            }
            bmp.Invalidate();
        }

        private static void BlendInto(byte[] px, int idx, byte b, byte g, byte r, byte a)
        {
            if (a == 255) { px[idx] = b; px[idx + 1] = g; px[idx + 2] = r; px[idx + 3] = 255; return; }
            byte da = px[idx + 3];
            if (da == 0) { px[idx] = b; px[idx + 1] = g; px[idx + 2] = r; px[idx + 3] = a; return; }
            float af = a / 255f;
            px[idx] = (byte)(b * af + px[idx] * (1 - af));
            px[idx + 1] = (byte)(g * af + px[idx + 1] * (1 - af));
            px[idx + 2] = (byte)(r * af + px[idx + 2] * (1 - af));
            px[idx + 3] = (byte)Math.Min(255, da + a);
        }

        private static void DrawSegmentInto(byte[] px, int stride, int w, int h, Point a, Point b, Color c, int half)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max((int)len * 2, 2);
            for (int s = 0; s <= steps; s++)
            {
                double f = (double)s / steps;
                int cx = (int)(a.X + dx * f), cy = (int)(a.Y + dy * f);
                for (int oy = -half; oy <= half; oy++)
                    for (int ox = -half; ox <= half; ox++)
                    {
                        int x = cx + ox, y = cy + oy;
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        BlendInto(px, y * stride + x * 4, c.B, c.G, c.R, c.A);
                    }
            }
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
