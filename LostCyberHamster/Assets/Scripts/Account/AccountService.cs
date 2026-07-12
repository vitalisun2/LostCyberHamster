using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LostCyberHamster.Account
{
    public sealed class AccountService : IAccountService
    {
        public static AccountService Instance { get; } = new AccountService(
            new UnityAuthenticationGateway(),
            new UnityPlayerAccountGateway());

        private readonly IUnityAuthenticationGateway _authenticationGateway;
        private readonly IUnityPlayerAccountGateway _playerAccountGateway;

        internal AccountService(
            IUnityAuthenticationGateway authenticationGateway,
            IUnityPlayerAccountGateway playerAccountGateway)
        {
            _authenticationGateway = authenticationGateway;
            _playerAccountGateway = playerAccountGateway;
        }

        public event Action<AccountSnapshot> StateChanged;

        public AccountSnapshot Snapshot { get; private set; } = AccountSnapshot.Unknown;

        /// <summary>
        /// Восстанавливает сохранённую UGS-сессию или создаёт гостевую и публикует актуальное состояние аккаунта.
        /// </summary>
        public async Task<AccountSnapshot> EnsureSignedInAsync()
        {
            try
            {
                await _authenticationGateway.InitializeAsync();

                if (!_authenticationGateway.IsSignedIn)
                {
                    await _authenticationGateway.SignInAnonymouslyAsync();
                }

                return await RefreshLinkStateAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to sign in: {ex.Message}");
                return SetSnapshot(AccountState.Offline, Snapshot.PlayerId, Snapshot.IsSignedIn, Snapshot.IsLinked, ex.Message);
            }
        }

        /// <summary>
        /// Повторно запрашивает связь текущего UGS-игрока с Unity Player Account и публикует новый snapshot.
        /// </summary>
        public async Task<AccountSnapshot> RefreshLinkStateAsync()
        {
            try
            {
                if (!_authenticationGateway.IsSignedIn)
                {
                    return SetSnapshot(AccountState.Unknown, string.Empty, false, false, string.Empty);
                }

                var isLinked = await _authenticationGateway.IsUnityAccountLinkedAsync();
                var state = isLinked ? AccountState.Linked : AccountState.Guest;
                return SetSnapshot(state, _authenticationGateway.PlayerId, true, isLinked, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to refresh account state: {ex.Message}");
                return SetSnapshot(AccountState.Error, Snapshot.PlayerId, Snapshot.IsSignedIn, Snapshot.IsLinked, ex.Message);
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
        public async Task<AccountLinkResult> LinkUnityAccountAsync()
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

            // Link не должен молча менять Player ID: конфликт обрабатывает вызывающий UX.
            var result = await _authenticationGateway.LinkWithUnityAsync(accessToken);

            if (result.IsSuccess)
            {
                await RefreshLinkStateAsync();
            }

            return result;
        }

        /// <summary>
        /// Отвязывает Unity Player Account и публикует обновлённое состояние текущего UGS-игрока.
        /// </summary>
        public async Task<AccountSnapshot> UnlinkUnityAccountAsync()
        {
            try
            {
                await _authenticationGateway.UnlinkUnityAsync();
                return await RefreshLinkStateAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountService] Failed to unlink account: {ex.Message}");
                return SetSnapshot(AccountState.Error, Snapshot.PlayerId, Snapshot.IsSignedIn, Snapshot.IsLinked, ex.Message);
            }
        }

        private AccountSnapshot SetSnapshot(
            AccountState state,
            string playerId,
            bool isSignedIn,
            bool isLinked,
            string errorMessage)
        {
            Snapshot = new AccountSnapshot(state, playerId, isSignedIn, isLinked, errorMessage);
            StateChanged?.Invoke(Snapshot);
            return Snapshot;
        }
    }
}
