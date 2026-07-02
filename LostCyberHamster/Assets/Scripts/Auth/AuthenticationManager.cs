using System;
using System.Threading.Tasks;
using LostCyberHamster.Account;

public static class AuthenticationManager
{
    public static event Action LinkingCompletedSuccess;

    public static event Action LinkingCompletedFailed;

    public static async Task SignInCachedUserAsync()
    {
        await AccountServiceProvider.Current.EnsureSignedInAsync();
    }

    public static async Task LinkAnonymousAccountToUnityAsync()
    {
        var result = await AccountServiceProvider.Current.LinkUnityAccountAsync();
        NotifyLinkCompleted(result);
    }

    public static async Task LinkWithUnityAsync(string accessToken)
    {
        var result = await AccountServiceProvider.Current.LinkUnityAccountWithAccessTokenAsync(accessToken);
        NotifyLinkCompleted(result);
    }

    public static async Task UnlinkUnityAsync()
    {
        await AccountServiceProvider.Current.UnlinkUnityAccountAsync();
    }

    public static async Task<bool> IsUnityAccountLinkedAsync()
    {
        return await AccountServiceProvider.Current.IsLinkedAsync();
    }

    private static void NotifyLinkCompleted(AccountLinkResult result)
    {
        if (result.IsSuccess)
        {
            LinkingCompletedSuccess?.Invoke();
        }
        else
        {
            LinkingCompletedFailed?.Invoke();
        }
    }
}
