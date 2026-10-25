using System;
using Windows.Web.Http; // v1.4.0.34: System.Net.Http facade lives only in the NETSTANDARD1.3 package graph that broke the XamlCompiler type universe (WMC1006); Windows.Web.Http ships with the UWP SDK winmd itself and is already used by AuthService/OneDriveService.
using System.Threading.Tasks;

namespace Barometer_UWP.Services
{
    public class UpdateService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/ArtemITuser/Barometer_UWP/releases/latest";

        public async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Barometer-UWP-App");
                    
                    var response = await httpClient.GetStringAsync(new Uri(GITHUB_API_URL));
                    // v1.4.0.34: vendored parser replaces Newtonsoft.Json (see Helpers\JsonSerializer.cs)
                    string tag, rname, url; DateTime published;
                    Barometer_UWP.Helpers.Json.ParseRelease(response, out tag, out rname, out url, out published);
                    if (string.IsNullOrEmpty(tag)) return null;

                    return new ReleaseInfo
                    {
                        Version = tag,
                        Name = rname,
                        Url = url,
                        PublishedAt = published
                    };
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public class ReleaseInfo
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public DateTime PublishedAt { get; set; }
    }

}