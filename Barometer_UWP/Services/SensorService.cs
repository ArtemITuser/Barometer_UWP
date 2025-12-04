using System;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using Barometer_UWP.Models;

namespace Barometer_UWP.Services
{
    public class SensorService
    {
        private Barometer _barometer;
        public PressureRecord LastReading { get; private set; }
        
        public event Action<PressureRecord> OnReading;

        public async Task<bool> InitializeAsync()
        {
            _barometer = Barometer.GetDefault();
            if (_barometer != null)
            {
                _barometer.ReadingChanged += OnReadingChanged;
                return true;
            }
            return false;
        }

        private void OnReadingChanged(Barometer sender, BarometerReadingChangedEventArgs args)
        {
            if (args.Reading != null)
            {
                var record = new PressureRecord
                {
                    Timestamp = DateTime.Now,
                    PressureHpa = args.Reading.StationPressureInHectopascals
                };
                
                LastReading = record;
                OnReading?.Invoke(record);
            }
        }

        public void SetReportInterval(uint milliseconds)
        {
            if (_barometer != null)
            {
                _barometer.ReportInterval = Math.Max(_barometer.MinimumReportInterval, milliseconds);
            }
        }

        public void Dispose()
        {
            if (_barometer != null)
            {
                _barometer.ReadingChanged -= OnReadingChanged;
                _barometer = null;
            }
        }
    }
}