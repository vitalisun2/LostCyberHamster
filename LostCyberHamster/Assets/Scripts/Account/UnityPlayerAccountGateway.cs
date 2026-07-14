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
        /// <summary>
        /// Запускает вход и возвращает access token после успешного завершения flow.
        /// </summary>
        public async Task<string> SignInAsync()
        {
            var service = PlayerAccountService.Instance;
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
    }
}
