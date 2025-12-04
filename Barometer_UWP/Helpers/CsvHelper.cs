using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Barometer_UWP.Helpers
{
    public static class CsvHelper
    {
        public static async Task<string> GenerateCsvContentAsync(IEnumerable<Models.PressureRecord> records, CultureInfo culture = null)
        {
            culture = culture ?? CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            
            // Add header
            sb.AppendLine("Timestamp,Pressure_hPa,Pressure_mmHg");
            
            // Add records
            foreach (var record in records)
            {
                sb.AppendLine($"{record.Timestamp:o},{record.PressureHpa.ToString(culture)},{record.PressureMmHg.ToString(culture)}");
            }
            
            return sb.ToString();
        }

        public static async Task SaveToCsvFileAsync(IEnumerable<Models.PressureRecord> records, StorageFile file, CultureInfo culture = null)
        {
            var csvContent = await GenerateCsvContentAsync(records, culture);
            await FileIO.WriteTextAsync(file, csvContent);
        }
    }
}