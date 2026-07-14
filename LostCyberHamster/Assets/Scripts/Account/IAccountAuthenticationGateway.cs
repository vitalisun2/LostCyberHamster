using System.Threading.Tasks;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Предоставляет AccountService минимальный доступ к Unity Authentication и связи Unity Player Account.
    /// </summary>
    public interface IAccountAuthenticationGateway
    {
        bool SessionTokenExists { get; }

        bool IsUnityPlayerAccountLinked { get; }

        bool IsSignedIn { get; }

        string PlayerId { get; }

        Task SignInAnonymouslyAsync(bool createAccount);

        Task<AccountLinkResult> LinkWithUnityAsync(string accessToken);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Входит в существующий UGS-аккаунт по Unity Player Account без создания нового аккаунта.
        /// </summary>
        Task SignInWithUnityAsync(string accessToken);

        /// <summary>
        /// Удаляет связь текущего UGS-аккаунта с Unity Player Account.
        /// </summary>
        Task UnlinkUnityAsync();
#endif

        void SignOutAndClearLocalCredentials();
    }
}
