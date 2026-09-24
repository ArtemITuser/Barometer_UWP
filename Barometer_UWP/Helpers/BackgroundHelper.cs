using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Barometer_UWP.Helpers
{
    public static class BackgroundHelper
    {
        /// <summary>
        /// Applies Mica or Acrylic background effect to a UI element
        /// </summary>
        /// <param name="element">The UI element to apply the effect to</param>
        /// <param name="useMica">Whether to use Mica (true) or Acrylic (false)</param>
        public static void ApplyBackgroundEffect(FrameworkElement element, bool useMica = true)
        {
            try
            {
                if (useMica)
                {
                    ApplyMicaBackground(element);
                }
                else
                {
                    ApplyAcrylicBackground(element);
                }
            }
            catch
            {
                // Fallback to solid color if effects are not supported
                element.Background = Application.Current.Resources["SystemControlBackgroundAltHighBrush"] as Brush;
            }
        }

        private static void ApplyMicaBackground(FrameworkElement element)
        {
            // For UWP on Windows 10, Mica is not natively supported in the same way as WinUI 3
            // We'll use Acrylic as a close approximation to Mica
            ApplyAcrylicBackground(element);
        }

        private static void ApplyAcrylicBackground(FrameworkElement element)
        {
            // Create an acrylic brush as background
            var acrylicBrush = new AcrylicBrush
            {
                BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                TintColor = Colors.White,
                TintOpacity = 0.6,
                FallbackColor = Colors.LightGray
            };

            element.Background = acrylicBrush;
        }

        /// <summary>
        /// Gets the appropriate background brush based on theme
        /// </summary>
        /// <returns>Brush for the current theme</returns>
        public static Brush GetThemedBackgroundBrush()
        {
            var theme = Application.Current.RequestedTheme;
            var key = theme == Application.UI.Xaml.ApplicationTheme.Dark 
                ? "SystemControlAcrylicElementBrush" 
                : "SystemControlAcrylicElementLightBrush";
            
            return Application.Current.Resources[key] as Brush ?? 
                   Application.Current.Resources["SystemControlBackgroundAltHighBrush"] as Brush;
        }
    }
}