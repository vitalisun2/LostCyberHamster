using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    internal sealed class UnityPlayerAccountGateway : IUnityPlayerAccountGateway
    {
        public async Task<string> SignInAndGetAccessTokenAsync()
        {
            if (PlayerAccountService.Instance.IsSignedIn && !string.IsNullOrEmpty(PlayerAccountService.Instance.AccessToken))
            {
                return PlayerAccountService.Instance.AccessToken;
            }

            var completion = new TaskCompletionSource<string>();

            void Unsubscribe()
            {
                PlayerAccountService.Instance.SignedIn -= HandleSignedIn;
                PlayerAccountService.Instance.SignInFailed -= HandleSignInFailed;
            }

            void HandleSignedIn()
            {
                Unsubscribe();

                var accessToken = PlayerAccountService.Instance.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    completion.TrySetException(new InvalidOperationException("Unity Player Account returned an empty access token."));
                    return;
                }

                completion.TrySetResult(accessToken);
            }

            void HandleSignInFailed(RequestFailedException ex)
            {
                Unsubscribe();
                completion.TrySetException(ex);
            }

            PlayerAccountService.Instance.SignedIn += HandleSignedIn;
            PlayerAccountService.Instance.SignInFailed += HandleSignInFailed;

            try
            {
                await PlayerAccountService.Instance.StartSignInAsync();

                if (completion.Task.IsCompleted)
                {
                    return await completion.Task;
                }

                if (!string.IsNullOrEmpty(PlayerAccountService.Instance.AccessToken))
                {
                    Unsubscribe();
                    return PlayerAccountService.Instance.AccessToken;
                }

                return await completion.Task;
            }
            catch
            {
                Unsubscribe();
                throw;
            }
        }
    }
}
