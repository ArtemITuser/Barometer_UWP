using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Graph;
using Windows.Storage;

namespace Barometer_UWP.Services
{
    public class OneDriveService
    {
        private GraphServiceClient _graphClient;
        private AuthService _authService;

        public OneDriveService(AuthService authService)
        {
            _authService = authService;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                var token = await _authService.GetAccessTokenSilentAsync(new[] { "User.Read", "Files.ReadWrite.AppFolder" });
                if (!string.IsNullOrEmpty(token))
                {
                    var authProvider = new DelegateAuthenticationProvider(
                        requestMessage =>
                        {
                            requestMessage.Headers.Authorization = 
                                new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
                            return Task.CompletedTask;
                        });
                    
                    _graphClient = new GraphServiceClient(authProvider);
                    return true;
                }
            }
            catch (Exception)
            {
                // Failed to initialize
            }
            
            return false;
        }

        public async Task<bool> UploadBackupAsync(string filename, byte[] data)
        {
            if (_graphClient == null)
                return false;

            try
            {
                var fileStream = new MemoryStream(data);
                var uploadedItem = await _graphClient.Me.Drive.AppRoot
                    .ItemWithPath(filename)
                    .Content
                    .Request()
                    .PutAsync<DriveItem>(fileStream);

                return uploadedItem != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<byte[]> DownloadBackupAsync(string filename)
        {
            if (_graphClient == null)
                return null;

            try
            {
                var stream = await _graphClient.Me.Drive.AppRoot
                    .ItemWithPath(filename)
                    .Content
                    .Request()
                    .GetAsync();

                using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<string>> ListBackupsAsync()
        {
            var backups = new List<string>();
            
            if (_graphClient == null)
                return backups;

            try
            {
                var items = await _graphClient.Me.Drive.AppRoot
                    .Children
                    .Request()
                    .Filter("name hasExtension 'json' or name hasExtension 'xlsx'")
                    .GetAsync();

                foreach (var item in items)
                {
                    backups.Add(item.Name);
                }
            }
            catch (Exception)
            {
                // Handle exception
            }

            return backups;
        }
    }
}