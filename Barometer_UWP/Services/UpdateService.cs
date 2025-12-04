using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
                    
                    var response = await httpClient.GetStringAsync(GITHUB_API_URL);
                    var release = JsonConvert.DeserializeObject<GitHubRelease>(response);
                    
                    return new ReleaseInfo
                    {
                        Version = release.tag_name,
                        Name = release.name,
                        Url = release.html_url,
                        PublishedAt = release.published_at
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

    // Internal class to match GitHub API response
    internal class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string html_url { get; set; }
        public DateTime published_at { get; set; }
    }
}