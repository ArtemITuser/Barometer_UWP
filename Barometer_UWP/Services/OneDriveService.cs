using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Barometer_UWP.Services
{
    /// <summary>
    /// OneDrive backups via Microsoft Graph REST (App Root special folder),
    /// authenticated with an MSA token from AuthService. No SDK dependencies,
    /// fully compatible with Windows 10 Mobile ARM (15063+).
    /// </summary>
    public sealed class OneDriveService
    {
        private const string GraphBase = "https://graph.microsoft.com/v1.0";
        private const string AppRootChildren = GraphBase + "/me/drive/special/approot/children";
        private const string AppRootItem = GraphBase + "/me/drive/special/approot/";

        private readonly AuthService _auth;

        public OneDriveService(AuthService auth)
        {
            _auth = auth;
        }

        public bool IsAvailable => _auth != null && _auth.IsSignedIn;

        private async Task<string> GetTokenAsync() => await _auth.GetAccessTokenAsync();

        private static HttpClient CreateClient(string token)
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            http.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
            return http;
        }

        /// <summary>Uploads (or replaces) a backup file in the OneDrive app folder.</summary>
        public async Task<bool> UploadBackupAsync(string filename, byte[] data)
        {
            var token = await GetTokenAsync();
            if (token == null || data == null) return false;

            try
            {
                using (var http = CreateClient(token))
                {
                    var content = new ByteArrayContent(data);
                    content.Headers.ContentType =
                        new HttpMediaTypeHeaderValue("application/octet-stream") { CharSet = "utf-8" };
                    var url = AppRootItem + Uri.EscapeDataString(filename) + ":/content";
                    var resp = await http.PutAsync(new Uri(url), content);
                    return resp.IsSuccessStatusCode;
                }
            }
            catch { return false; }
        }

        /// <summary>Downloads a backup file from the OneDrive app folder.</summary>
        public async Task<byte[]> DownloadBackupAsync(string filename)
        {
            var token = await GetTokenAsync();
            if (token == null) return null;

            try
            {
                using (var http = CreateClient(token))
                {
                    var url = AppRootItem + Uri.EscapeDataString(filename) + ":/content";
                    var resp = await http.GetBufferAsync(new Uri(url));
                    if (!resp.IsSuccessStatusCode) return null;
                    var buffer = await resp.Content.ReadAsBufferAsync();
                    var bytes = new byte[buffer.Length];
                    using (var reader = DataReader.FromBuffer(buffer))
                        reader.ReadBytes(bytes);
                    return bytes;
                }
            }
            catch { return null; }
        }

        /// <summary>Lists backup files (*.json / *.xlsx) stored in the app folder.</summary>
        public async Task<List<CloudBackupInfo>> ListBackupsAsync()
        {
            var result = new List<CloudBackupInfo>();
            var token = await GetTokenAsync();
            if (token == null) return result;

            try
            {
                using (var http = CreateClient(token))
                {
                    var next = AppRootChildren + "?$select=name,size,lastModifiedDateTime&$top=200";
                    while (!string.IsNullOrEmpty(next))
                    {
                        var resp = await http.GetStringAsync(new Uri(next));
                        var page = JObject.Parse(resp);
                        foreach (var item in page["value"] ?? new JArray())
                        {
                            var name = item["name"]?.ToString();
                            if (string.IsNullOrEmpty(name)) continue;
                            if (!(name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                  name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))) continue;
                            DateTime.TryParse(item["lastModifiedDateTime"]?.ToString(), out var modified);
                            result.Add(new CloudBackupInfo
                            {
                                Name = name,
                                SizeBytes = item["size"]?.Value<long>() ?? 0,
                                LastModified = modified
                            });
                        }
                        next = page["@odata.nextLink"]?.ToString();
                    }
                }
            }
            catch { }

            return result.OrderByDescending(b => b.LastModified).ToList();
        }
    }

    public class CloudBackupInfo
    {
        public string Name { get; set; }
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
    }
}
