using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Оркестрирует сценарии UGS-сессии и привязки Unity Player Account, публикуя актуальное состояние аккаунта.
    /// </summary>
    public sealed class AccountService : IAccountService
    {
        private readonly IUnityAuthenticationGateway _authenticationGateway;
        private readonly IUnityPlayerAccountGateway _playerAccountGateway;
        private readonly object _linkSync = new object();
        private readonly object _stateSync = new object();
        private readonly SemaphoreSlim _stateOperationGate = new SemaphoreSlim(1, 1);
        private Task<AccountLinkResult> _activeLinkTask;
        private AccountSnapshot _snapshot = AccountSnapshot.Unknown;

        internal AccountService(
            IUnityAuthenticationGateway authenticationGateway,
            IUnityPlayerAccountGateway playerAccountGateway)
        {
            _authenticationGateway = authenticationGateway ??
                throw new ArgumentNullException(nameof(authenticationGateway));
            _playerAccountGateway = playerAccountGateway ??
                throw new ArgumentNullException(nameof(playerAccountGateway));
        }

        public event Action<AccountSnapshot> StateChanged;

        public AccountSnapshot Snapshot
        {
            get
            {
                lock (_stateSync)
                {
                    return _snapshot;
                }
            }
        }

        /// <summary>
        /// Восстанавливает сохранённую UGS-сессию или создаёт гостевую и публикует актуальное состояние аккаунта.
        /// </summary>
        public async Task<AccountSnapshot> EnsureSignedInAsync()
        {
            await _stateOperationGate.WaitAsync();

            try
            {
                await _authenticationGateway.InitializeAsync();

                if (!_authenticationGateway.IsSignedIn)
                {
                    await _authenticationGateway.SignInAnonymouslyAsync();
                }

                return await RefreshLinkStateCoreAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to sign in: {ex.Message}");
                AccountState state = IsOfflineFailure(ex) ? AccountState.Offline : AccountState.Error;
                return SetFailureSnapshot(state, ex.Message);
            }
            finally
            {
                _stateOperationGate.Release();
            }
        }

        /// <summary>
        /// Повторно запрашивает связь текущего UGS-игрока с Unity Player Account и публикует новый snapshot.
        /// </summary>
        public async Task<AccountSnapshot> RefreshLinkStateAsync()
        {
            await _stateOperationGate.WaitAsync();

            try
            {
                return await RefreshLinkStateCoreAsync();
            }
            finally
            {
                _stateOperationGate.Release();
            }
        }

        private async Task<AccountSnapshot> RefreshLinkStateCoreAsync()
        {
            try
            {
                if (!_authenticationGateway.IsSignedIn)
                {
                    return SetSnapshot(
                        AccountState.Unknown,
                        string.Empty,
                        false,
                        false,
                        string.Empty);
                }

                var isLinked = await _authenticationGateway.IsUnityAccountLinkedAsync();
                var state = isLinked ? AccountState.Linked : AccountState.Guest;
                return SetSnapshot(
                    state,
                    _authenticationGateway.PlayerId,
                    true,
                    isLinked,
                    string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to refresh account state: {ex.Message}");
                return SetFailureSnapshot(AccountState.Error, ex.Message);
            }
        }

        /// <summary>
        /// Гарантирует наличие UGS-сессии и возвращает признак связи с Unity Player Account.
        /// </summary>
        public async Task<bool> IsLinkedAsync()
        {
            var snapshot = _authenticationGateway.IsSignedIn
                ? await RefreshLinkStateAsync()
                : await EnsureSignedInAsync();

            return snapshot.IsLinked;
        }

        /// <summary>
        /// Получает access token и связывает с ним текущего UGS-игрока без смены Player ID при конфликте.
        /// </summary>
        public Task<AccountLinkResult> LinkUnityAccountAsync()
        {
            lock (_linkSync)
            {
                if (_activeLinkTask == null || _activeLinkTask.IsCompleted)
                {
                    var completion = new TaskCompletionSource<AccountLinkResult>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _activeLinkTask = completion.Task;
                    _ = ProduceLinkResultAsync(completion);
                }

                return _activeLinkTask;
            }
        }

        private async Task ProduceLinkResultAsync(TaskCompletionSource<AccountLinkResult> completion)
        {
            try
            {
                completion.TrySetResult(await LinkUnityAccountCoreAsync());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }

        private async Task<AccountLinkResult> LinkUnityAccountCoreAsync()
        {
            var snapshot = await EnsureSignedInAsync();
            if (!snapshot.IsSignedIn)
            {
                return AccountLinkResult.Failed(snapshot.ErrorMessage);
            }

            if (snapshot.IsLinked)
            {
                return AccountLinkResult.Success(snapshot.PlayerId);
            }

            try
            {
                var accessToken = await _playerAccountGateway.SignInAndGetAccessTokenAsync();
                return await LinkUnityAccountWithAccessTokenAsync(accessToken);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to start account link: {ex.Message}");
                return AccountLinkResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Привязывает Unity Player Account к текущему UGS-игроку и возвращает конфликт без смены Player ID.
        /// </summary>
        public async Task<AccountLinkResult> LinkUnityAccountWithAccessTokenAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return AccountLinkResult.Failed("Unity access token is empty.");
            }

            await _stateOperationGate.WaitAsync();

            try
            {
                if (!_authenticationGateway.IsSignedIn)
                {
                    return AccountLinkResult.Failed("UGS player session is not signed in.");
                }

                // Link не должен молча менять Player ID: конфликт обрабатывает вызывающий UX.
                var result = await _authenticationGateway.LinkWithUnityAsync(accessToken);

                if (result.IsSuccess)
                {
                    await RefreshLinkStateCoreAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to link account: {ex.Message}");
                return AccountLinkResult.Failed(ex.Message);
            }
            finally
            {
                _stateOperationGate.Release();
            }
        }

        /// <summary>
        /// Отвязывает Unity Player Account и публикует обновлённое состояние текущего UGS-игрока.
        /// </summary>
        public async Task<AccountSnapshot> UnlinkUnityAccountAsync()
        {
            await _stateOperationGate.WaitAsync();

            try
            {
                if (!_authenticationGateway.IsSignedIn)
                {
                    return SetSnapshot(
                        AccountState.Unknown,
                        string.Empty,
                        false,
                        false,
                        string.Empty);
                }

                await _authenticationGateway.UnlinkUnityAsync();
                return await RefreshLinkStateCoreAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to unlink account: {ex.Message}");
                return SetFailureSnapshot(AccountState.Error, ex.Message);
            }
            finally
            {
                _stateOperationGate.Release();
            }
        }

        private AccountSnapshot SetFailureSnapshot(
            AccountState state,
            string errorMessage)
        {
            bool isSignedIn = _authenticationGateway.IsSignedIn;
            string playerId = isSignedIn ? _authenticationGateway.PlayerId : string.Empty;
            return SetSnapshot(
                state,
                playerId,
                isSignedIn,
                false,
                errorMessage);
        }

        private AccountSnapshot SetSnapshot(
            AccountState state,
            string playerId,
            bool isSignedIn,
            bool isLinked,
            string errorMessage)
        {
            AccountSnapshot snapshot;

            lock (_stateSync)
            {
                _snapshot = new AccountSnapshot(state, playerId, isSignedIn, isLinked, errorMessage);
                snapshot = _snapshot;
            }

            PublishStateChanged(snapshot);
            return snapshot;
        }

        private void PublishStateChanged(AccountSnapshot snapshot)
        {
            Delegate[] subscribers = StateChanged?.GetInvocationList();
            if (subscribers == null)
            {
                return;
            }

            foreach (Action<AccountSnapshot> subscriber in subscribers)
            {
                try
                {
                    subscriber(snapshot);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AccountService] StateChanged subscriber failed: {ex.Message}");
                }
            }
        }

        private static bool IsOfflineFailure(Exception exception)
        {
            return exception is TimeoutException ||
                   exception is RequestFailedException requestFailedException &&
                   requestFailedException.ErrorCode == CommonErrorCodes.TransportError;
        }
    }
}
