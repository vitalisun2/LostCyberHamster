using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Делегирует flow входа в Unity Player Accounts SDK.
    /// </summary>
    public sealed class UnityPlayerAccountGateway : IUnityPlayerAccountGateway
    {
        public bool IsSignedIn => PlayerAccountService.Instance.IsSignedIn;

        /// <summary>
        /// Возвращает access token текущей сессии или запускает вход и ожидает успешного завершения flow.
        /// </summary>
        public async Task<string> SignInAsync()
        {
            var service = PlayerAccountService.Instance;
            if (service.IsSignedIn && !string.IsNullOrWhiteSpace(service.AccessToken))
                return service.AccessToken;

            var completion = new TaskCompletionSource<string>();

            void Unsubscribe()
            {
                service.SignedIn -= OnSignedIn;
                service.SignInFailed -= OnSignInFailed;
            }

            void OnSignedIn()
            {
                Unsubscribe();
                completion.TrySetResult(service.AccessToken);
            }

            void OnSignInFailed(RequestFailedException exception)
            {
                Unsubscribe();
                completion.TrySetException(exception);
            }

            service.SignedIn += OnSignedIn;
            service.SignInFailed += OnSignInFailed;

            try
            {
                await service.StartSignInAsync();
            }
            catch
            {
                Unsubscribe();
                throw;
            }

            try
            {
                return await completion.Task;
            }
            finally
            {
                Unsubscribe();
            }
        }

        /// <summary>
        /// Завершает текущую локальную сессию Unity Player Accounts.
        /// </summary>
        public void SignOut()
        {
            PlayerAccountService.Instance.SignOut();
        }
    }
}
