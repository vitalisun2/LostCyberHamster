using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Делегирует авторизацию и управление связью аккаунта в Unity Authentication SDK.
    /// </summary>
    public sealed class UnityAccountAuthenticationGateway : IAccountAuthenticationGateway
    {
        public bool SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;

        public bool IsUnityPlayerAccountLinked => AuthenticationService.Instance.PlayerInfo?.GetUnityId() != null;

        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => AuthenticationService.Instance.PlayerId;

        /// <summary>
        /// Возвращает полное публичное имя текущего игрока.
        /// </summary>
        public string PlayerName => AuthenticationService.Instance.PlayerName;

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
