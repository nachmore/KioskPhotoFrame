using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KioskPhotoFrame
{
  // Helper library for access to to the MS Authentication Library
  // based on: https://docs.microsoft.com/en-us/azure/active-directory/develop/tutorial-v2-windows-uwp
  static class MSALHelper
  {
    private /*const*/ static string CLIENT_ID;
    private static readonly string[] SCOPES = { "user.read" };

    private static IPublicClientApplication _publicClientApp;
    private static string _accessToken;

    static MSALHelper()
    {
      // STOPGAP STOPGAP STOPGAP STOPGAP STOPGAP (prevents check in of the key, but need to distribute file. Waiting
      // for build solution)
      CLIENT_ID = File.ReadAllText("client.user");
      // STOPGAP STOPGAP STOPGAP STOPGAP STOPGAP

      _publicClientApp = PublicClientApplicationBuilder.Create(CLIENT_ID)
                          .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                          .WithLogging((level, message, containsPii) =>
                          {
                            Debug.WriteLine($"MSAL: {level} {message} ");
                          }, LogLevel.Warning, enablePiiLogging: false, enableDefaultPlatformLogging: true)
                          .WithUseCorporateNetwork(true)
                          .Build();
    }

    public static async void AcquireToken()
    {
      AuthenticationResult authResult = null;

      // It's good practice to not do work on the UI thread, so use ConfigureAwait(false) whenever possible.            
      IEnumerable<IAccount> accounts = await _publicClientApp.GetAccountsAsync().ConfigureAwait(false);
      IAccount firstAccount = accounts.FirstOrDefault();

      try
      {
        authResult = await _publicClientApp.AcquireTokenSilent(SCOPES, firstAccount).ExecuteAsync();
      }
      catch (MsalUiRequiredException ex)
      {
        // A MsalUiRequiredException happened on AcquireTokenSilent.
        // This indicates you need to call AcquireTokenInteractive to acquire a token
        Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

        try
        {
          authResult = await _publicClientApp.AcquireTokenInteractive(SCOPES).ExecuteAsync().ConfigureAwait(false);
        }
        catch (MsalException msalex)
        {
          Debug.WriteLine($"Error Acquiring Token:{System.Environment.NewLine}{msalex}");
          throw;
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}");
        throw;
      }

      _accessToken = authResult.AccessToken;

      Debug.WriteLine($"Access Token set to: {_accessToken}");
    }
  }
}
