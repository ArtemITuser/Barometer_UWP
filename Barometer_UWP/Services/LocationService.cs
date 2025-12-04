using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace Barometer_UWP.Services
{
    public class LocationService
    {
        private Geolocator _geolocator;

        public async Task<bool> RequestLocationPermissionAsync()
        {
            try
            {
                var accessStatus = await Geolocator.RequestAccessAsync();
                return accessStatus == GeolocationAccessStatus.Allowed;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Geoposition> GetCurrentLocationAsync()
        {
            try
            {
                if (_geolocator == null)
                {
                    _geolocator = new Geolocator();
                    _geolocator.DesiredAccuracy = PositionAccuracy.High;
                }

                return await _geolocator.GetGeopositionAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<SunriseSunsetInfo> GetSunriseSunsetAsync(double latitude, double longitude, DateTime date)
        {
            // Simplified calculation for sunrise/sunset
            // For a more accurate calculation, a full NOAA algorithm implementation would be needed
            // This is a basic approximation
            
            var dayOfYear = date.DayOfYear;
            var declination = 23.45 * Math.Sin(2 * Math.PI / 365 * (284 + dayOfYear));
            var hourAngle = Math.Acos(-Math.Tan(latitude * Math.PI / 180) * Math.Tan(declination * Math.PI / 180));
            var daylightHours = 2 * hourAngle * 180 / Math.PI / 15;
            var daylightMinutes = daylightHours * 60;
            
            // Approximate sunrise and sunset times
            var sunriseHour = 12 - daylightHours / 2;
            var sunsetHour = 12 + daylightHours / 2;
            
            var sunrise = new DateTime(date.Year, date.Month, date.Day, (int)sunriseHour, (int)((sunriseHour - Math.Floor(sunriseHour)) * 60), 0);
            var sunset = new DateTime(date.Year, date.Month, date.Day, (int)sunsetHour, (int)((sunsetHour - Math.Floor(sunsetHour)) * 60), 0);
            
            return new SunriseSunsetInfo
            {
                Sunrise = sunrise,
                Sunset = sunset
            };
        }
    }

    public class SunriseSunsetInfo
    {
        public DateTime Sunrise { get; set; }
        public DateTime Sunset { get; set; }
    }
}