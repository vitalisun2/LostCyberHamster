using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    internal sealed class UnityAuthenticationGateway : IUnityAuthenticationGateway
    {
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => AuthenticationService.Instance.PlayerId ?? string.Empty;

        public Task InitializeAsync()
        {
            return UnityServices.InitializeAsync();
        }

        public Task SignInAnonymouslyAsync()
        {
            return AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<bool> IsUnityAccountLinkedAsync()
        {
            var playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();
            return !string.IsNullOrEmpty(playerInfo.GetUnityId());
        }

        public async Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            try
            {
                await AuthenticationService.Instance.LinkWithUnityAsync(accessToken);
                return AccountLinkResult.Success(PlayerId);
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return AccountLinkResult.AlreadyLinked(ex.Message);
            }
            catch (AuthenticationException ex)
            {
                return AccountLinkResult.Failed(ex.Message);
            }
            catch (RequestFailedException ex)
            {
                return AccountLinkResult.Failed(ex.Message);
            }
        }

        public Task UnlinkUnityAsync()
        {
            return AuthenticationService.Instance.UnlinkUnityAsync();
        }
    }
}
