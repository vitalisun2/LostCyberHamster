using System;
using System.Threading.Tasks;
using Assets.Scripts.Online;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Делегирует flow входа в Unity Player Accounts SDK.
    /// </summary>
    public sealed class UnityPlayerAccountGateway : IUnityPlayerAccountGateway
    {
        private const double ForegroundSignInTimeoutSeconds = 120;
        private int _flowVersion;
        private TaskCompletionSource<string> _pendingSignIn;

        public bool IsSignedIn => PlayerAccountService.Instance.IsSignedIn;

        /// <summary>
        /// Ограничивает browser flow двумя минутами активного приложения и отменяет прежнее ожидание при повторе.
        /// </summary>
        public async Task<string> SignInAsync()
        {
            var service = PlayerAccountService.Instance;
            CancelPendingSignIn(service);
            if (service.IsSignedIn && !string.IsNullOrWhiteSpace(service.AccessToken))
                return service.AccessToken;

            var version = ++_flowVersion;
            var completion = _pendingSignIn = new TaskCompletionSource<string>();

            void Unsubscribe()
            {
                service.SignedIn -= OnSignedIn;
                service.SignInFailed -= OnSignInFailed;
            }

            void OnSignedIn()
            {
                if (version == _flowVersion) completion.TrySetResult(service.AccessToken);
            }

            void OnSignInFailed(RequestFailedException exception)
            {
                if (version == _flowVersion) completion.TrySetException(exception);
            }

            service.SignedIn += OnSignedIn;
            service.SignInFailed += OnSignInFailed;

            try
            {
                // Windows ждёт callback внутри StartSignInAsync; deadline покрывает и этот этап.
                _ = ObserveLaunchAsync();
                double foregroundElapsed = 0;
                double previousTick = UnityGameClock.Instance.RealtimeSeconds;
                while (!completion.Task.IsCompleted)
                {
                    double now = UnityGameClock.Instance.RealtimeSeconds;
                    if (Application.isFocused)
                        foregroundElapsed += Math.Min(1, Math.Max(0, now - previousTick));
                    previousTick = now;
                    if (foregroundElapsed >= ForegroundSignInTimeoutSeconds)
                        throw new TimeoutException("Player Account sign-in timed out.");
                    await Task.Yield();
                }
                var accessToken = await completion.Task;
                if (version != _flowVersion) throw new OperationCanceledException("Player Account flow was superseded.");
                return accessToken;
            }
            catch
            {
                // SDK отменяет generation и закрывает listener; поздний OAuth callback будет проигнорирован.
                if (version == _flowVersion)
                {
                    _flowVersion++;
                    service.SignOut();
                }
                throw;
            }
            finally
            {
                Unsubscribe();
                if (ReferenceEquals(_pendingSignIn, completion)) _pendingSignIn = null;
            }

            async Task ObserveLaunchAsync()
            {
                try { await service.StartSignInAsync(); }
                catch (Exception exception)
                {
                    if (version == _flowVersion) completion.TrySetException(exception);
                }
            }
        }

        /// <summary>
        /// Завершает текущую локальную сессию Unity Player Accounts.
        /// </summary>
        public void SignOut()
        {
            var service = PlayerAccountService.Instance;
            if (!CancelPendingSignIn(service)) service.SignOut();
        }

        private bool CancelPendingSignIn(IPlayerAccountService service)
        {
            if (_pendingSignIn == null) return false;
            var previous = _pendingSignIn;
            _pendingSignIn = null;
            _flowVersion++;
            service.SignOut();
            previous.TrySetCanceled();
            return true;
        }
    }
}
