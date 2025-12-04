using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Barometer_UWP.Models;
using Barometer_UWP.Helpers;
using Windows.Storage;

namespace Barometer_UWP.Services
{
    public class ExportService
    {
        public async Task ExportCsvAsync(StorageFile file, CultureInfo culture = null)
        {
            var dataService = App.Current.Services.DataService;
            var records = dataService.GetAll();
            await CsvHelper.SaveToCsvFileAsync(records, file, culture);
        }

        public async Task ExportTxtAsync(StorageFile file, CultureInfo culture = null)
        {
            culture = culture ?? CultureInfo.InvariantCulture;
            var dataService = App.Current.Services.DataService;
            var records = dataService.GetAll();
            
            var content = "";
            content += "Timestamp\tPressure_hPa\tPressure_mmHg" + Environment.NewLine;
            
            foreach (var record in records)
            {
                content += $"{record.Timestamp:o}\t{record.PressureHpa.ToString(culture)}\t{record.PressureMmHg.ToString(culture)}" + Environment.NewLine;
            }
            
            await FileIO.WriteTextAsync(file, content);
        }

        public async Task ExportXlsxAsync(StorageFile file, CultureInfo culture = null)
        {
            var dataService = App.Current.Services.DataService;
            var records = dataService.GetAll();
            await ExcelHelper.SaveToExcelFileAsync(records, file);
        }

        public async Task ImportCsvAsync(StorageFile file, CultureInfo culture = null)
        {
            culture = culture ?? CultureInfo.InvariantCulture;
            var dataService = App.Current.Services.DataService;
            
            var content = await FileIO.ReadTextAsync(file);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length >= 3)
                {
                    if (DateTime.TryParse(parts[0], culture, DateTimeStyles.RoundTripKind, out var timestamp) &&
                        double.TryParse(parts[1], NumberStyles.Number, culture, out var pressureHpa))
                    {
                        var record = new PressureRecord
                        {
                            Timestamp = timestamp,
                            PressureHpa = pressureHpa,
                            Source = "import"
                        };
                        
                        // Avoid duplicates by checking if record with same timestamp already exists
                        var existingRecord = dataService.GetAll().Find(r => r.Timestamp == record.Timestamp);
                        if (existingRecord == null)
                        {
                            await dataService.AddAsync(record);
                        }
                    }
                }
            }
        }

        public async Task<byte[]> RenderPlotToPngAsync(object model, int width, int height)
        {
            // This would require OxyPlot implementation which is complex for this step
            // Placeholder for now
            return new byte[0];
        }
    }
}