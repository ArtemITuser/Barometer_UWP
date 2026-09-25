using Windows.ApplicationModel.Resources;

namespace Barometer_UWP.Strings
{
    /// <summary>
    /// Central access point for localized string resources
    /// (Strings\ru-RU\Resources.resw and Strings\en-US\Resources.resw).
    /// </summary>
    public static class ResourceLoaderProvider
    {
        private static readonly ResourceLoader Loader = ResourceLoader.GetForCurrentView();

        public static string Get(string key)
        {
            var value = Loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        // Frequently used strings (typed shortcuts)
        public static string AppTitle => Get("AppTitle");
        public static string CurrentPressure => Get("CurrentPressure");
        public static string Graphics => Get("Graphics");
        public static string Settings => Get("Settings");
        public static string Share => Get("Share");
        public static string MmHg => Get("UnitMmHg");
        public static string Hpa => Get("UnitHpa");
    }
}
