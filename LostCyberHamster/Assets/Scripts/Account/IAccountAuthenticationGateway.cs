using System.Threading.Tasks;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Предоставляет AccountService минимальный доступ к гостевой Unity Authentication.
    /// </summary>
    public interface IAccountAuthenticationGateway
    {
        bool SessionTokenExists { get; }

        bool IsUnityPlayerAccountLinked { get; }

        Task SignInAnonymouslyAsync(bool createAccount);

        void SignOutAndClearLocalCredentials();
    }
}
