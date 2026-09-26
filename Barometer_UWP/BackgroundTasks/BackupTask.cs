using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.Streams;
using Barometer_UWP.Services;
using Barometer_UWP.Helpers;

namespace Barometer_UWP.BackgroundTasks
{
    /// <summary>
    /// Periodic backup background task (TimeTrigger, min. 15 min).
    /// Creates a local JSON backup via DataService and, if the user is
    /// signed in to their Microsoft account, uploads it to OneDrive App Root.
    /// </summary>
    public sealed class BackupTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();

            try
            {
                // Honor cancellation / resource limits gracefully.
                if (taskInstance.CancellationReason == BackgroundTaskCancellationReason.SystemPolicy)
                {
                    return;
                }

                var settings = ApplicationData.Current.LocalSettings;
                bool autoLocal = !settings.Values.ContainsKey("AutoBackupLocal") || (bool)settings.Values["AutoBackupLocal"];
                bool autoCloud = settings.Values.ContainsKey("AutoBackupOneDrive") && (bool)settings.Values["AutoBackupOneDrive"];

                if (!autoLocal && !autoCloud)
                {
                    return; // backups disabled by user
                }

                var dataService = new DataService();
                await dataService.InitializeAsync();

                // Local JSON backup with metadata (single source of truth: DataService).
                var backup = await dataService.CreateLocalBackupAsync();
                if (backup == null || backup.File == null)
                {
                    await Logger.LogAsync("BackupTask: local backup skipped (no data or error).");
                    return;
                }

                // Cloud upload only when explicitly enabled and authenticated.
                if (autoCloud)
                {
                    try
                    {
                        var authService = new AuthService();
                        if (authService.IsSignedIn)
                        {
                            var oneDrive = new OneDriveService(authService);
                            var buffer = await FileIO.ReadBufferAsync(backup.File);
                            byte[] bytes;
                            using (var reader = new DataReader(buffer))
                            {
                                bytes = new byte[reader.UnconsumedBufferLength];
                                reader.ReadBytes(bytes);
                            }

                            bool uploaded = await oneDrive.UploadBackupAsync(backup.Name, bytes);
                            await Logger.LogAsync(uploaded
                                ? $"BackupTask: uploaded {backup.Name} to OneDrive."
                                : $"BackupTask: OneDrive upload failed for {backup.Name}.");
                        }
                        else
                        {
                            await Logger.LogAsync("BackupTask: OneDrive backup requested but user not signed in.");
                        }
                    }
                    catch (Exception cloudEx)
                    {
                        // Cloud failure must never break the local backup.
                        await Logger.LogErrorAsync("BackupTask: OneDrive step failed", cloudEx);
                    }
                }
            }
            catch (Exception ex)
            {
                try { await Logger.LogErrorAsync("Backup task failed", ex); } catch { }
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
