using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Делегирует авторизацию и управление связью аккаунта в Unity Authentication SDK.
    /// </summary>
    public sealed class UnityAccountAuthenticationGateway : IAccountAuthenticationGateway, IAccountSessionStatus, IAccountProfileGateway
    {
        public bool SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;

        public bool IsUnityPlayerAccountLinked => AuthenticationService.Instance.PlayerInfo?.GetUnityId() != null;

        public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

        public bool IsAuthorized => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsAuthorized;

        public event Action SessionExpired
        {
            add => AuthenticationService.Instance.Expired += value;
            remove => AuthenticationService.Instance.Expired -= value;
        }

        public string PlayerId => AuthenticationService.Instance.PlayerId;
        public string Profile => AuthenticationService.Instance.Profile;

        /// <summary>Выбирает изолированные credentials через публичный API Unity Authentication.</summary>
        public void SwitchProfile(string profile)
        {
            var service = AuthenticationService.Instance;
            if (service.Profile == profile) return;
            service.SignOut(clearCredentials: false);
            service.SwitchProfile(profile);
        }

        /// <summary>
        /// Возвращает полное публичное имя текущего игрока.
        /// </summary>
        public string PlayerName => UnityServices.State == ServicesInitializationState.Initialized
            ? AuthenticationService.Instance.PlayerName : null;

        public async Task SignInAnonymouslyAsync(bool createAccount)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync(new SignInOptions
            {
                CreateAccount = createAccount
            });
            await EnsurePlayerNameAsync();
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
                await EnsurePlayerNameAsync();
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

        /// <summary>
        /// Обновляет публичное имя текущего игрока и возвращает сохранённое полное имя.
        /// </summary>
        public Task<string> UpdatePlayerNameAsync(string playerName)
        {
            return AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        }

        /// <summary>
        /// Входит в существующий UGS-аккаунт без создания нового аккаунта.
        /// </summary>
        public async Task SignInWithUnityAsync(string accessToken)
        {
            await AuthenticationService.Instance.SignInWithUnityAsync(accessToken, new SignInOptions
            {
                CreateAccount = false
            });
            await EnsurePlayerNameAsync();
        }

        public void SignOutPreservingCredentials()
        {
            AuthenticationService.Instance.SignOut(clearCredentials: false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Удаляет связь текущего UGS-аккаунта с Unity Player Account.
        /// </summary>
        public Task UnlinkUnityAsync()
        {
            return AuthenticationService.Instance.UnlinkUnityAsync();
        }
#endif

        public void SignOutAndClearLocalCredentials()
        {
            AuthenticationService.Instance.SignOut(clearCredentials: true);
        }

        /// <summary>
        /// Получает сохранённое имя игрока или поручает Unity создать его автоматически.
        /// </summary>
        private static async Task EnsurePlayerNameAsync()
        {
            // Уже загруженное имя не требует повторного запроса.
            if (!string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerName))
                return;

            // Ошибка имени не отменяет успешно установленную игровую сессию.
            try
            {
                await AuthenticationService.Instance.GetPlayerNameAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Account] Player name resolution failed. Error type: {exception.GetType().Name}.");
            }
        }
    }
}
