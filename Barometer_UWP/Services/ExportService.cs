using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Windows.Storage;

namespace Barometer_UWP.Services
{
    public class ExportService
    {
        private DataService Data => App.Current?.Services?.DataService;

        public async Task ExportCsvAsync(StorageFile file, IEnumerable<PressureRecord> records = null, CultureInfo culture = null)
        {
            if (file == null) return;
            records = records ?? Data?.GetAll() ?? Enumerable.Empty<PressureRecord>();
            await CsvHelper.SaveToCsvFileAsync(records, file, culture);
        }

        public async Task ExportTxtAsync(StorageFile file, IEnumerable<PressureRecord> records = null, CultureInfo culture = null)
        {
            if (file == null) return;
            culture = culture ?? CultureInfo.InvariantCulture;
            records = records ?? Data?.GetAll() ?? Enumerable.Empty<PressureRecord>();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp\tPressure_hPa\tPressure_mmHg");
            foreach (var r in records)
                sb.AppendLine($"{r.Timestamp:o}\t{r.PressureHpa.ToString(culture)}\t{r.PressureMmHg.ToString(culture)}");
            await FileIO.WriteTextAsync(file, sb.ToString());
        }

        public async Task ExportXlsxAsync(StorageFile file, IEnumerable<PressureRecord> records = null, CultureInfo culture = null)
        {
            if (file == null) return;
            records = records ?? Data?.GetAll() ?? Enumerable.Empty<PressureRecord>();
            await ExcelHelper.SaveToExcelFileAsync(records, file);
        }

        /// <summary>Imports CSV/TXT (comma or tab separated). Dedupes by timestamp. Returns count added.</summary>
        public async Task<int> ImportDelimitedAsync(StorageFile file, CultureInfo culture = null)
        {
            if (file == null || Data == null) return 0;
            culture = culture ?? CultureInfo.InvariantCulture;
            var content = await FileIO.ReadTextAsync(file);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var incoming = new List<PressureRecord>();

            for (int i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split(new[] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                // Skip header
                if (i == 0 && !DateTime.TryParseExact(parts[0].Trim(), "o", culture, DateTimeStyles.RoundtripKind, out _))
                    continue;

                if (DateTime.TryParse(parts[0].Trim(), culture, DateTimeStyles.RoundtripKind, out var ts))
                {
                    double hpa;
                    if (parts.Length >= 3 && double.TryParse(parts[1].Trim(), NumberStyles.Number, culture, out hpa))
                    {
                        incoming.Add(new PressureRecord { Timestamp = ts, PressureHpa = hpa, Source = "import" });
                    }
                    else if (double.TryParse(parts.Last().Trim(), NumberStyles.Number, culture, out var mmhg))
                    {
                        incoming.Add(new PressureRecord { Timestamp = ts, PressureHpa = UnitConverter.MmHgToHpa(mmhg), Source = "import" });
                    }
                }
            }
            return await Data.AddRangeAsync(incoming);
        }

        public Task<int> ImportCsvAsync(StorageFile file, CultureInfo culture = null) =>
            ImportDelimitedAsync(file, culture);

        public async Task<int> ImportXlsxAsync(StorageFile file)
        {
            if (file == null || Data == null) return 0;
            var records = await ExcelHelper.LoadFromExcelFileAsync(file);
            return await Data.AddRangeAsync(records);
        }

        /// <summary>Exports a chart image as an HTML document with embedded base64 PNG.</summary>
        public async Task ExportPlotHtmlAsync(StorageFile file, byte[] pngBytes, string title)
        {
            if (file == null || pngBytes == null) return;
            var b64 = Convert.ToBase64String(pngBytes);
            var html = $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>{System.Net.WebUtility.HtmlEncode(title)}</title></head>" +
                       $"<body style=\"font-family:Segoe UI,sans-serif\"><h3>{System.Net.WebUtility.HtmlEncode(title)}</h3>" +
                       $"<img src=\"data:image/png;base64,{b64}\" alt=\"pressure chart\"/></body></html>";
            await FileIO.WriteTextAsync(file, html);
        }
    }
}
