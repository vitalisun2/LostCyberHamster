using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace Assets.Scripts.Account
{
    public enum AccountState
    {
        NotStarted,
        Resolving,
        Guest,
        Error
    }

    public sealed class AccountService
    {
        private int _resolutionVersion;

        public AccountState State { get; private set; } = AccountState.NotStarted;

        /// <summary>
        /// Переводит аккаунт в состояние определения гостя и запускает его без блокировки игры.
        /// </summary>
        public void Start()
        {
            // Не запускаем определение аккаунта повторно.
            if (State != AccountState.NotStarted)
                return;

            // Публикуем начало определения аккаунта.
            State = AccountState.Resolving;
            Debug.Log("[Account] State: Resolving");

            // Запускаем незавершённую пока логику гостя без ожидания.
            var resolutionVersion = ++_resolutionVersion;
            _ = ResolveGuestAsync(resolutionVersion);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Очищает только локальные Unity Authentication credentials и возвращает сервис в начальное состояние.
        /// </summary>
        public void ResetForTesting()
        {
            _resolutionVersion++;
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            State = AccountState.NotStarted;
            Debug.Log("[Account] Test state reset. Local credentials cleared.");
        }
#endif

        /// <summary>
        /// Восстанавливает существующего гостя или создаёт нового по локальной сессии.
        /// </summary>
        private async Task ResolveGuestAsync(int resolutionVersion)
        {
            try
            {
                // Выбираем ровно один сценарий по наличию локальной сессии.
                var authenticationService = AuthenticationService.Instance;
                var restoreGuest = authenticationService.SessionTokenExists;

                if (restoreGuest)
                    Debug.Log("[Account] Scenario selected: RestoreGuest.");
                else
                    Debug.Log("[Account] Scenario selected: CreateGuest.");

                // Выполняем только выбранный сценарий без fallback на создание.
                await authenticationService.SignInAnonymouslyAsync(new SignInOptions
                {
                    CreateAccount = !restoreGuest
                });

                if (resolutionVersion != _resolutionVersion)
                {
                    authenticationService.SignOut(clearCredentials: true);
                    return;
                }

                State = AccountState.Guest;
                Debug.Log(restoreGuest
                    ? "[Account] Guest restored."
                    : "[Account] Guest created.");
            }
            catch (Exception exception)
            {
                if (resolutionVersion != _resolutionVersion)
                    return;

                State = AccountState.Error;
                Debug.LogError($"[Account] Guest resolution failed: {exception.Message}");
            }
        }
    }
}
