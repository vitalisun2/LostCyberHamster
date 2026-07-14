using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Делегирует гостевую авторизацию в Unity Authentication SDK.
    /// </summary>
    public sealed class UnityAccountAuthenticationGateway : IAccountAuthenticationGateway
    {
        public bool SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;

        public bool IsUnityPlayerAccountLinked => AuthenticationService.Instance.PlayerInfo?.GetUnityId() != null;

        public Task SignInAnonymouslyAsync(bool createAccount)
        {
            return AuthenticationService.Instance.SignInAnonymouslyAsync(new SignInOptions
            {
                CreateAccount = createAccount
            });
        }

        public void SignOutAndClearLocalCredentials()
        {
            AuthenticationService.Instance.SignOut(clearCredentials: true);
        }
    }
}
