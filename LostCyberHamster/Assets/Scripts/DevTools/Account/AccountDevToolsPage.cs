#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Перечисляет страницы account-раздела DEV-меню.
    /// </summary>
    internal enum AccountDevToolsPage
    {
        Account,
        Sessions,
        Diagnostics,
        HelpIndex,
        HelpDetail,
        Confirmation
    }
}
#endif
