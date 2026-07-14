using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Account
{

    public sealed class AccountService
    {
        private readonly IAccountAuthenticationGateway _authenticationGateway;
        private int _resolutionVersion;

        public AccountState State { get; private set; } = AccountState.NotStarted;

        public AccountService(IAccountAuthenticationGateway authenticationGateway)
        {
            _authenticationGateway = authenticationGateway
                ?? throw new ArgumentNullException(nameof(authenticationGateway));
        }

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
            _authenticationGateway.SignOutAndClearLocalCredentials();
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
                var restoreGuest = _authenticationGateway.SessionTokenExists;

                if (restoreGuest)
                    Debug.Log("[Account] Scenario selected: RestoreGuest.");
                else
                    Debug.Log("[Account] Scenario selected: CreateGuest.");

                // Выполняем только выбранный сценарий без fallback на создание.
                await _authenticationGateway.SignInAnonymouslyAsync(createAccount: !restoreGuest);

                if (resolutionVersion != _resolutionVersion)
                {
                    _authenticationGateway.SignOutAndClearLocalCredentials();
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
