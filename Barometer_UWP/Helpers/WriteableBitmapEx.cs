using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;

namespace Barometer_UWP.Helpers
{
    /// <summary>
    /// Minimal self-contained WriteableBitmap drawing helpers (no external
    /// WriteableBitmapEx package required — works on UAP 15063 / Win10 Mobile).
    /// </summary>
    public static class WriteableBitmapExtensions
    {
        public static void FillRectangle(this WriteableBitmap bmp, Rect rect, Color color)
        {
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            Fill(pixels, stride, w, h, rect, color);
            CopyTo(bmp, pixels);
        }

        private static void Fill(byte[] px, int stride, int w, int h, Rect r, Color c)
        {
            int x0 = Math.Max(0, (int)Math.Floor(r.X));
            int y0 = Math.Max(0, (int)Math.Floor(r.Y));
            int x1 = Math.Min(w, (int)Math.Ceiling(r.X + r.Width));
            int y1 = Math.Min(h, (int)Math.Ceiling(r.Y + r.Height));
            byte b = c.B, g = c.G, rr = c.R, a = c.A;
            for (int y = y0; y < y1; y++)
            {
                int idx = y * stride + x0 * 4;
                for (int x = x0; x < x1; x++, idx += 4)
                {
                    px[idx] = b; px[idx + 1] = g; px[idx + 2] = rr; px[idx + 3] = a;
                }
            }
        }

        /// <summary>Draws an anti-aliasing-free polyline with thickness into the bitmap.</summary>
        public static void DrawLineThick(this WriteableBitmap bmp, IList<Point> pts, Color color, double thickness)
        {
            if (pts == null || pts.Count < 2) return;
            int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
            byte[] px = ReadFrom(bmp, stride, h);

            int t = Math.Max(1, (int)Math.Round(thickness));
            int half = t / 2;
            for (int i = 0; i < pts.Count - 1; i++)
                DrawSegment(px, stride, w, h, pts[i], pts[i + 1], color, half);

            CopyTo(bmp, px);
        }

        private static void DrawSegment(byte[] px, int stride, int w, int h, Point a, Point b, Color c, int half)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max((int)len * 2, 2);
            byte bb = c.B, bg = c.G, br = c.R, ba = c.A;
            for (int s = 0; s <= steps; s++)
            {
                double f = (double)s / steps;
                int cx = (int)(a.X + dx * f), cy = (int)(a.Y + dy * f);
                for (int oy = -half; oy <= half; oy++)
                    for (int ox = -half; ox <= half; ox++)
                    {
                        int x = cx + ox, y = cy + oy;
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        int idx = y * stride + x * 4;
                        BlendPixel(px, idx, bb, bg, br, ba);
                    }
            }
        }

        private static void BlendPixel(byte[] px, int idx, byte b, byte g, byte r, byte a)
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

        private static byte[] ReadFrom(WriteableBitmap bmp, int stride, int h)
        {
            var arr = new byte[stride * h];
            using (var stream = bmp.OpenStream())
                stream.Read(arr, 0, arr.Length);
            return arr;
        }

        private static void CopyTo(WriteableBitmap bmp, byte[] arr)
        {
            using (var stream = bmp.OpenStream())
            {
                stream.Position = 0;
                stream.Write(arr, 0, arr.Length);
                stream.Flush();
            }
            bmp.Invalidate();
        }
    }

    public static class ChartRendererBitmap
    {
        // placeholder to keep type names tidy
    }
}
