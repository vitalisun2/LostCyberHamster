using System;
using System.Threading.Tasks;
using LostCyberHamster.Account;

public static class AuthenticationManager
{
    public static event Action LinkingCompletedSuccess;

    public static event Action LinkingCompletedFailed;

    /// <summary>
    /// Совместимый фасад для восстановления сохранённой UGS-сессии или гостевого входа.
    /// </summary>
    public static async Task SignInCachedUserAsync()
    {
        await AccountServiceProvider.Current.EnsureSignedInAsync();
    }

    /// <summary>
    /// Совместимый фасад для безопасной привязки текущего UGS-игрока без смены Player ID при конфликте.
    /// </summary>
    public static async Task LinkAnonymousAccountToUnityAsync()
    {
        var result = await AccountServiceProvider.Current.LinkUnityAccountAsync();
        NotifyLinkCompleted(result);
    }

    /// <summary>
    /// Совместимый фасад для безопасной привязки Unity Player Account по заранее полученному access token.
    /// </summary>
    public static async Task LinkWithUnityAsync(string accessToken)
    {
        var result = await AccountServiceProvider.Current.LinkUnityAccountWithAccessTokenAsync(accessToken);
        NotifyLinkCompleted(result);
    }

    /// <summary>
    /// Совместимый фасад для отвязки Unity Player Account от текущего UGS-игрока.
    /// </summary>
    public static async Task UnlinkUnityAsync()
    {
        await AccountServiceProvider.Current.UnlinkUnityAccountAsync();
    }

    /// <summary>
    /// Совместимый фасад для проверки связи текущего UGS-игрока с Unity Player Account.
    /// </summary>
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
