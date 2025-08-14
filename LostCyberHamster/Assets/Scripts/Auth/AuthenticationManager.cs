using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;

public static class AuthenticationManager
{

    public static event Action LinkingCompletedSuccess;

    public static event Action LinkingCompletedFailed;

    public static async Task SignInCachedUserAsync()
    {
        // Check if a cached player already exists by checking if the session token exists
        if (!AuthenticationService.Instance.SessionTokenExists)
        {
            Debug.Log("No cached player found.");
            // if not, then do nothing
            return;
        }

        // Sign in Anonymously
        // This call will sign in the cached player.
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Sign in anonymously succeeded!");

            // Shows how to get the playerID
            Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            Debug.LogException(ex);
        }
    }

    public static async Task LinkAnonymousAccountToUnityAsync()
    {
        // Subscribe to the SignedIn event to handle linking
        PlayerAccountService.Instance.SignedIn += OnUnitySignIn;
        try
        {
            if (PlayerAccountService.Instance.IsSignedIn)
            {
                Debug.Log("Player is already signed in.");
            }
            // Open the browser for Unity Player Account sign-in
            Debug.Log("Starting Unity Player Account sign-in.");
            await PlayerAccountService.Instance.StartSignInAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start Unity Player Account sign-in.");
            Debug.LogException(ex);
        }
    }

    // Event handler for when the player completes Unity sign-in
    private static async void OnUnitySignIn()
    {
        Debug.Log("Unity Player Account sign-in complete.");
        // Unsubscribe from the event to prevent duplicate calls
        PlayerAccountService.Instance.SignedIn -= OnUnitySignIn;

        // Get the Unity Player Account access token
        string accessToken = PlayerAccountService.Instance.AccessToken;

        // Link the anonymous account to the Unity Player Account
        await LinkWithUnityAsync(accessToken);
    }



    public static async Task LinkWithUnityAsync(string accessToken)
    {
        try
        {
            await AuthenticationService.Instance.LinkWithUnityAsync(accessToken);
            Debug.Log("Account successfully linked to Unity Player Account.");
            LinkingCompletedSuccess?.Invoke();
        }
        catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
        {
            Debug.LogError("This user is already linked with another account. Log in instead.");
            LinkingCompletedSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            LinkingCompletedFailed?.Invoke();
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            LinkingCompletedFailed?.Invoke();
        }
    }

    public static async Task UnlinkUnityAsync()
    {
        try
        {
            await AuthenticationService.Instance.UnlinkUnityAsync();
            Debug.Log("Unlink is successful.");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }


    public static async Task<bool> IsUnityAccountLinkedAsync()
    {
        try
        {
            // Fetch player info
            var playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();

            // Check if the player has a Unity Player Account linked
            foreach (var identity in playerInfo.Identities)
            {
                if (identity.TypeId == "unity")
                {
                    Debug.Log("Player has a Unity Player Account linked.");
                    return true;
                }
            }

            Debug.Log("Player does not have a Unity Player Account linked.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("Error fetching player info.");
            //Debug.LogException(ex);
            return false;
        }
    }
}
