using System;
using System.Collections.Generic;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Newtonsoft.Json;

namespace Barometer_UWP.Services
{
    /// <summary>
    /// MSA sign-in via OAuth 2.0 authorization-code flow using the system
    /// WebAuthenticationCoreManager broker (supports 2FA / security keys —
    /// the app NEVER sees or stores the user's password).
    /// Tokens are cached in LocalSettings and refreshed via the refresh token.
    /// </summary>
    public sealed class AuthService
    {
        // NOTE: register your app in Azure AD (Personal MSAs + accounts in any org),
        // add redirect URI "https://login.microsoftonline.com/common/oauth2/nativeclient"
        // and replace this placeholder ClientId before release.
        public const string ClientId = "REPLACE_WITH_AZURE_APP_CLIENT_ID";

        private const string AuthorizeEndpoint = "https://login.live.com/oauth20_authorize.srf";
        private const string TokenEndpoint = "https://login.live.com/oauth20_token.srf";
        private const string RedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private const string Scopes = "wl.signin wl.offline_access onedrive.appfolder";

        private const string SettingsTokenKey = "msa_access_token";
        private const string SettingsRefreshKey = "msa_refresh_token";
        private const string SettingsExpiresKey = "msa_expires_utc";

        public bool IsSignedIn => !string.IsNullOrEmpty(GetRefreshToken()) ||
                                  (!string.IsNullOrEmpty(GetAccessToken()) && !TokenExpired());

        public event Action AuthStateChanged;

        public async System.Threading.Tasks.Task<bool> SignInAsync()
        {
            try
            {
                var uri = new Uri(
                    $"{AuthorizeEndpoint}?client_id={ClientId}&scope={Uri.EscapeDataString(Scopes)}" +
                    $"&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUri)}");

                var result = await WebAuthenticationCoreManager.GetTokenAsync(
                    new WebAuthenticationOptions(WebAuthenticationPromptOptions.PromptAlways),
                    new Uri("https://login.microsoftonline.com"),
                    uri,
                    new List<KeyValuePair<string, string>>());

                if (!result.ResponseStatus.HasFlag(WebAuthenticationStatus.Success))
                    return false;

                // The broker returns either a token directly or an authorization code
                // depending on provider response parsing; handle both.
                var response = result.ResponseData;
                var parsed = TryParseTokenResponse(response);
                if (parsed != null)
                {
                    SaveTokens(parsed);
                    AuthStateChanged?.Invoke();
                    return true;
                }

                // Broker may return an authorization code — exchange it for tokens.
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                    if (dict != null && dict.TryGetValue("code", out var authCode) && !string.IsNullOrEmpty(authCode))
                        return await RedeemCodeAsync(authCode);
                }
                catch { }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async System.Threading.Tasks.Task<bool> RedeemCodeAsync(string authCode)
        {
            using (var http = new Windows.Web.Http.HttpClient())
            {
                var form = new Windows.Web.Http.Filters.HttpFormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("client_id", ClientId),
                    new KeyValuePair<string,string>("scope", Scopes),
                    new KeyValuePair<string,string>("code", authCode),
                    new KeyValuePair<string,string>("grant_type", "authorization_code"),
                    new KeyValuePair<string,string>("redirect_uri", RedirectUri),
                });

                var resp = await http.PostAsync(new Uri(TokenEndpoint), form);
                var body = await resp.Content.ReadAsStringAsync();
                var token = TryParseTokenResponse(body);
                if (token != null)
                {
                    SaveTokens(token);
                    AuthStateChanged?.Invoke();
                    return true;
                }
                return false;
            }
        }

        /// <summary>Returns a valid access token, refreshing silently when needed.</summary>
        public async System.Threading.Tasks.Task<string> GetAccessTokenAsync()
        {
            if (IsSignedIn)
            {
                if (!TokenExpired() && !string.IsNullOrEmpty(GetAccessToken()))
                    return GetAccessToken();

                var refreshed = await RefreshAsync();
                if (refreshed) return GetAccessToken();
            }
            return await SignInAsync() ? GetAccessToken() : null;
        }

        public async System.Threading.Tasks.Task<bool> RefreshAsync()
        {
            var refresh = GetRefreshToken();
            if (string.IsNullOrEmpty(refresh)) return false;
            try
            {
                using (var http = new Windows.Web.Http.HttpClient())
                {
                    var content = new Windows.Web.Http.Filters.HttpFormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string,string>("client_id", ClientId),
                        new KeyValuePair<string,string>("scope", Scopes),
                        new KeyValuePair<string,string>("refresh_token", refresh),
                        new KeyValuePair<string,string>("grant_type", "refresh_token"),
                    });

                    var resp = await http.PostAsync(new Uri(TokenEndpoint), content);
                    var body = await resp.Content.ReadAsStringAsync();
                    var token = TryParseTokenResponse(body);
                    if (token != null)
                    {
                        SaveTokens(token);
                        AuthStateChanged?.Invoke();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public void SignOut()
        {
            var s = ApplicationData.Current.LocalSettings.Values;
            s.Remove(SettingsTokenKey);
            s.Remove(SettingsRefreshKey);
            s.Remove(SettingsExpiresKey);
            AuthStateChanged?.Invoke();
        }

        private static TokenResponse TryParseTokenResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var t = JsonConvert.DeserializeObject<TokenResponse>(json);
                return (t != null && !string.IsNullOrEmpty(t.AccessToken)) ? t : null;
            }
            catch { return null; }
        }

        private static void SaveTokens(TokenResponse t)
        {
            var s = ApplicationData.Current.LocalSettings.Values;
            s[SettingsTokenKey] = t.AccessToken;
            if (!string.IsNullOrEmpty(t.RefreshToken)) s[SettingsRefreshKey] = t.RefreshToken;
            s[SettingsExpiresKey] = DateTime.UtcNow.AddSeconds(t.ExpiresIn - 60).ToString("o");
        }

        private static string GetAccessToken() =>
            ApplicationData.Current.LocalSettings.Values[SettingsTokenKey] as string;

        private static string GetRefreshToken() =>
            ApplicationData.Current.LocalSettings.Values[SettingsRefreshKey] as string;

        private static bool TokenExpired()
        {
            var exp = ApplicationData.Current.LocalSettings.Values[SettingsExpiresKey] as string;
            return string.IsNullOrEmpty(exp) || DateTime.Parse(exp).ToUniversalTime() <= DateTime.UtcNow;
        }

        private class TokenResponse
        {
            [JsonProperty("access_token")] public string AccessToken { get; set; }
            [JsonProperty("refresh_token")] public string RefreshToken { get; set; }
            [JsonProperty("expires_in")] public long ExpiresIn { get; set; }
        }
    }
}
