using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Barometer_UWP.Services;
using Barometer_UWP.Models;
using Barometer_UWP.Helpers;
using Newtonsoft.Json;

namespace Barometer_UWP.BackgroundTasks
{
    public sealed class BackupTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            
            try
            {
                // Get the data service to access pressure records
                var dataService = new DataService();
                await dataService.InitializeAsync();
                
                var records = dataService.GetAll();
                
                // Create local backup
                var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var backupFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                
                var backupMetadata = new BackupMetadata
                {
                    Filename = fileName,
                    CreatedAt = DateTime.Now,
                    Storage = "Local"
                };
                
                var json = JsonConvert.SerializeObject(new { Records = records, Metadata = backupMetadata }, Formatting.Indented);
                await FileIO.WriteTextAsync(backupFile, json);
                
                // Try to upload to OneDrive if authenticated
                var authService = new AuthService();
                var oneDriveService = new OneDriveService(authService);
                
                if (await oneDriveService.InitializeAsync())
                {
                    var fileContent = await FileIO.ReadBufferAsync(backupFile);
                    var bytes = new byte[fileContent.Length];
                    using (var reader = DataReader.FromBuffer(fileContent))
                    {
                        reader.ReadBytes(bytes);
                    }
                    
                    await oneDriveService.UploadBackupAsync(fileName, bytes);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogErrorAsync("Backup task failed", ex);
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}