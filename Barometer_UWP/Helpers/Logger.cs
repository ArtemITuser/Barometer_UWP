using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Barometer_UWP.Helpers
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static StorageFile _logFile;

        public static async Task InitializeAsync()
        {
            _logFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("log.txt", CreationCollisionOption.OpenIfExists);
        }

        public static async Task LogAsync(string message)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                await FileIO.AppendTextAsync(_logFile, logMessage);
            }
            catch
            {
                // Ignore logging errors to prevent recursive logging issues
            }
        }

        public static async Task LogErrorAsync(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message} - Exception: {ex}" : message;
            await LogAsync($"ERROR: {fullMessage}");
        }

        public static async Task ClearLogAsync()
        {
            if (_logFile != null)
            {
                await FileIO.WriteTextAsync(_logFile, string.Empty);
            }
        }
    }
}