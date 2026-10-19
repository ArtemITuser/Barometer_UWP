using System;

namespace Barometer_UWP.Models
{
    public class PressureRecord
    {
        public DateTime Timestamp { get; set; }
        public double PressureHpa { get; set; }

        // XAML display helpers (UWP classic Binding has no StringFormat — WMC0011).
        public string TimeText => Timestamp.ToString("HH:mm:ss");
        public string HpaText => PressureHpa.ToString("F2") + " hPa";
        public string MmHgText => PressureMmHg.ToString("F2") + " mmHg";
        
        public double PressureMmHg
        {
            get { return PressureHpa * 0.75006375541921; }
        }
        
        public string Source { get; set; } = "sensor"; // Default source is sensor
    }
}