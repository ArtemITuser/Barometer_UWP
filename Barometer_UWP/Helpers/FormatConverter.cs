using System;
using Windows.UI.Xaml.Data;

namespace Barometer_UWP.Helpers
{
    /// <summary>
    /// UWP classic Binding has no StringFormat (XamlCompiler error WMC0011).
    /// This converter provides formatting via ConverterParameter instead.
    /// Usage: Text="{x:Bind Value, Mode=OneWay, Converter={StaticResource FormatConverter}, ConverterParameter=F2}"
    /// Supported parameter prefixes: "F<n>" numeric format, "t" short time, "g" general date/time,
    /// or any standard DateTime format string ("HH:mm", "yyyy-MM-dd").
    /// </summary>
    public sealed class FormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string fmt = parameter as string ?? "F2";
            try
            {
                if (value is DateTime dt)
                {
                    switch (fmt)
                    {
                        case "t": return dt.ToString("t");
                        case "g": return dt.ToString("g");
                        default: return dt.ToString(fmt);
                    }
                }
                if (value is double d) return d.ToString(fmt);
                if (value is int i) return i.ToString(fmt);
                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return value?.ToString() ?? string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
