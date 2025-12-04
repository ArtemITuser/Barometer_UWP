using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Barometer_UWP.Services
{
    public class AuthService
    {
        private IPublicClientApplication _clientApplication;
        private const string CLIENT_ID = "YOUR_CLIENT_ID_HERE"; // To be replaced with actual client ID
        private const string REDIRECT_URI = "ms-appx-web://Microsoft.AAD.BrokerPlugin/YOUR_CLIENT_ID_HERE";
        
        public AuthService()
        {
            _clientApplication = PublicClientApplicationBuilder
                .Create(CLIENT_ID)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient") // Standard redirect for UWP
                .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.PersonalMicrosoftAccountAndOtherOrganizations)
                .Build();
        }

        public async Task<bool> SignInAsync()
        {
            try
            {
                var accounts = await _clientApplication.GetAccountsAsync();
                var firstAccount = accounts.FirstOrDefault();

                if (firstAccount != null)
                {
                    // Try silent authentication first
                    try
                    {
                        var authResult = await _clientApplication.AcquireTokenSilent(new[] { "user.read" }, firstAccount)
                            .ExecuteAsync();
                        return true;
                    }
                    catch (MsalUiRequiredException)
                    {
                        // Silent auth failed, need to show interactive dialog
                    }
                }

                // Fallback to interactive authentication
                var result = await _clientApplication.AcquireTokenInteractive(new[] { "user.read" })
                    .WithUseEmbeddedWebView(true)
                    .ExecuteAsync();

                return result != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task SignOutAsync()
        {
            var accounts = await _clientApplication.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _clientApplication.RemoveAsync(account);
            }
        }

        public async Task<string> GetAccessTokenSilentAsync(string[] scopes)
        {
            try
            {
                var accounts = await _clientApplication.GetAccountsAsync();
                var firstAccount = accounts.FirstOrDefault();

                if (firstAccount != null)
                {
                    var authResult = await _clientApplication.AcquireTokenSilent(scopes, firstAccount)
                        .ExecuteAsync();
                    return authResult.AccessToken;
                }
            }
            catch (MsalUiRequiredException)
            {
                // Silent auth failed, need to show interactive dialog
            }

            return null;
        }
    }
}