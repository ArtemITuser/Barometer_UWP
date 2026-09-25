using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Media.Imaging;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;

namespace Barometer_UWP.Services
{
    /// <summary>
    /// Live-tile rendering (RC1):
    ///  - square/wide tiles always show the big rounded mmHg number + trend arrow;
    ///  - wide (310x150) and large tiles additionally get a mini pressure chart
    ///    rendered to PNG in LocalFolder and referenced via ms-appdata:///local/...
    ///  - period (1h/6h/24h/7d), chart on/off and style (system/custom color)
    ///    are read from LocalSettings written by SettingsViewModel.
    /// Updates are rate-limited by the caller (MainViewModel).
    /// </summary>
    public class TileService
    {
        private const string TileImageName = "tile_chart.png";
        private DateTime _lastRenderAttempt = DateTime.MinValue;

        // ---- settings helpers -------------------------------------------------
        private static ApplicationDataContainer S => ApplicationData.Current.LocalSettings;
        private static string TilePeriod => (S.Values["SelectedTilePeriod"] as string) ?? "24h";
        private static bool ShowChart => (S.Values["ShowChartOnTile"] as bool?) ?? true;
        private static string TileStyle => (S.Values["SelectedTileStyle"] as string) ?? "System";
        private static string TileAccentHex => (S.Values["TileCustomColor"] as string) ?? "#00A0FF";

        private static TimeSpan PeriodSpan()
        {
            switch (TilePeriod)
            {
                case "1h": return TimeSpan.FromHours(1);
                case "6h": return TimeSpan.FromHours(6);
                case "7d": return TimeSpan.FromDays(7);
                default: return TimeSpan.FromDays(1);
            }
        }

        private static Color AccentColor()
        {
            if (TileStyle == "Custom")
            {
                try
                {
                    if (hex.Length < 6) throw new FormatException();
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
                catch { /* fall through to system accent */ }
            }
            try
            {
                return (Color)Windows.UI.Xaml.Application.Current.Resources[
                    "SystemAccentColor"];
            }
            catch { return Color.FromArgb(255, 0, 120, 215); }
        }

        // ---- public API -------------------------------------------------------

        /// <summary>Updates all tile sizes with the latest reading (pressure is hPa — source of truth).</summary>
        public async Task UpdateTileAsync(PressureRecord record)
        {
            if (record == null) return;
            try
            {
                double mmHg = record.PressureMmHg;
                string big = Math.Round(mmHg).ToString();
                string unit = "мм рт. ст.";

                // Square 150x150 — number only (Text01, single line, centered look).
                Push(Template(TileTemplateType.TileSquare150x150Text01), (doc, text) =>
                {
                    text[0].InnerText = big;
                });

                // Wide 310x150 — big number + caption (+ optional chart image).
                string imgSrc = await TryRenderChartAsync();
                Push(Template(TileTemplateType.TileWide310x150BlockAndText01), (doc, text) =>
                {
                    text[0].InnerText = big;          // smallBusiness2 slot -> big number area
                    text[1].InnerText = unit;
                    text[2].InnerText = DateTime.Now.ToString("HH:mm");
                    text[3].InnerText = TrendArrow(record);
                    if (imgSrc != null) AddImage(doc, imgSrc);
                });

                // Fallback square with text lines for devices that ignore wide template.
                Push(Template(TileTemplateType.TileSquare150x150Text03), (doc, text) =>
                {
                    text[0].InnerText = $"{big} {unit}";
                    text[1].InnerText = TrendArrow(record);
                });
            }
            catch (Exception ex)
            {
                try { await Logger.LogErrorAsync("Tile update failed", ex); } catch { }
            }
        }

        /// <summary>Legacy overload: pressure assumed hPa unless unit says otherwise.</summary>
        public Task UpdateTileAsync(double pressure, string unit = "hPa")
        {
            double hpa = unit == "mmHg" ? pressure / 0.75006375541921 : pressure;
            return UpdateTileAsync(new PressureRecord
            {
                Timestamp = DateTime.Now,
                PressureHpa = hpa,
                Source = "sensor"
            });
        }

        public void ClearTileAsync()
        {
            try { TileUpdateManager.CreateTileUpdaterForApplication().Clear(); } catch { }
        }

        // ---- internals --------------------------------------------------------

        private static XmlDocument Template(TileTemplateType t) =>
            TileUpdateManager.GetTemplateContent(t);

        private static void Push(XmlDocument doc, Action<XmlDocument, IXmlNodeList> fill)
        {
            var text = doc.GetElementsByTagName("text");
            fill(doc, text);
            var notif = new TileNotification(doc);
            notif.ExpirationTime = DateTimeOffset.UtcNow.AddHours(12);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(notif);
        }

        private static string TrendArrow(PressureRecord record)
        {
            try
            {
                var all = App.Current?.Services?.DataService?.GetAll();
                if (all == null || all.Count < 5) return "\u2192";
                double delta = record.PressureMmHg - all[Math.Max(0, all.Count - 31)].PressureMmHg;
                return delta > 0.75 ? "\u25B2" : delta < -0.75 ? "\u25BC" : "\u2192";
            }
            catch { return "\u2192"; }
        }

        private static void AddImage(XmlDocument doc, string src)
        {
            var binding = doc.DocumentElement.SelectSingleNode("visual/binding");
            if (binding == null) return;
            var image = doc.CreateElement("image");
            var attr = doc.CreateAttribute("src");
            attr.Value = src;
            image.Attributes.SetNamedItem(attr);
            var alt = doc.CreateAttribute("alt");
            alt.Value = "pressure chart";
            image.Attributes.SetNamedItem(alt);
            binding.AppendChild(image);
        }

        /// <summary>Renders the mini chart for the configured period into LocalFolder; returns ms-appdata URI or null.</summary>
        private async Task<string> TryRenderChartAsync()
        {
            if (!ShowChart) return null;

            // Rate-limit heavy pixel work to once per minute.
            if ((DateTime.Now - _lastRenderAttempt).TotalSeconds < 60) return null;
            _lastRenderAttempt = DateTime.Now;

            try
            {
                var data = App.Current?.Services?.DataService;
                if (data == null) return null;
                var all = data.GetAll();
                var end = DateTime.Now;
                var start = end - PeriodSpan();
                var pts = all.Where(r => r.Timestamp >= start && r.Timestamp <= end)
                             .OrderBy(r => r.Timestamp)
                             .Select(r => new ChartPoint
                             {
                                 X = (r.Timestamp - start).TotalMinutes,
                                 Y = r.PressureMmHg
                             }).ToList();
                if (pts.Count < 2) return null;
                pts = ChartRenderer.Decimate(pts, 300);

                const int w = 300, h = 120;
                var bmp = new WriteableBitmap(w, h);
                using (var s = bmp.OpenStream()) { } // ensure back buffer allocated
                bmp.FillRectangle(new Windows.Foundation.Rect(0, 0, w, h), Colors.Transparent);
                ChartRenderer.DrawPolylineOnBitmap(bmp, pts, AccentColor(), 2.0);

                byte[] png = await ImageEncoder.EncodeAsync(bmp, asPng: true);
                if (png == null || png.Length == 0) return null;

                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(TileImageName,
                    CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBytesAsync(file, png);
                return "ms-appdata:///local/" + TileImageName;
            }
            catch { return null; }
        }
    }
}
