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

        public string PlayerId => AuthenticationService.Instance.PlayerId;

        public Task SignInAnonymouslyAsync(bool createAccount)
        {
            return AuthenticationService.Instance.SignInAnonymouslyAsync(new SignInOptions
            {
                CreateAccount = createAccount
            });
        }

        /// <summary>
        /// Привязывает текущую identity к Unity Player Account без принудительной замены связи.
        /// </summary>
        public async Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            try
            {
                await AuthenticationService.Instance.LinkWithUnityAsync(accessToken, new LinkOptions
                {
                    ForceLink = false
                });
                return AccountLinkResult.Linked;
            }
            catch (AuthenticationException exception)
                when (exception.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return AccountLinkResult.Conflict;
            }
            catch
            {
                return AccountLinkResult.Failed;
            }
        }

        public void SignOutAndClearLocalCredentials()
        {
            AuthenticationService.Instance.SignOut(clearCredentials: true);
        }
    }
}
