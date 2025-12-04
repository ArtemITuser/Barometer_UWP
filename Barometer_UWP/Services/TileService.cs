using System;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Barometer_UWP.Services
{
    public class TileService
    {
        public async Task UpdateTileAsync(double pressure, string unit = "mmHg")
        {
            try
            {
                var pressureValue = unit == "mmHg" ? 
                    pressure * 0.75006375541921 : // Convert hPa to mmHg if needed
                    pressure;

                // Create a small tile notification with just the pressure value
                var smallTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text01);
                var smallTileText = smallTileXml.GetElementsByTagName("text");
                smallTileText[0].InnerText = $"{pressureValue:F1}";
                
                var smallTileNotification = new TileNotification(smallTileXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(smallTileNotification);
                
                // Create a wide tile notification with pressure and unit
                var wideTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150Text09);
                var wideTileText = wideTileXml.GetElementsByTagName("text");
                wideTileText[0].InnerText = $"Pressure: {pressureValue:F1} {unit}";
                wideTileText[1].InnerText = DateTime.Now.ToString("HH:mm");
                
                var wideTileNotification = new TileNotification(wideTileXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(wideTileNotification);
            }
            catch (Exception)
            {
                // Handle tile update errors
            }
        }

        public async Task ClearTileAsync()
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }
    }
}