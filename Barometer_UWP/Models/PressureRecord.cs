using System;

namespace Barometer_UWP.Models
{
    public class PressureRecord
    {
        public DateTime Timestamp { get; set; }
        public double PressureHpa { get; set; }
        
        public double PressureMmHg
        {
            get { return PressureHpa * 0.75006375541921; }
        }
        
        public string Source { get; set; } = "sensor"; // Default source is sensor
    }
}