using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Account
{
    public enum AccountState
    {
        NotStarted,
        Resolving
    }

    public sealed class AccountService
    {
        public AccountState State { get; private set; } = AccountState.NotStarted;

        /// <summary>
        /// Переводит аккаунт в состояние определения гостя и запускает его без блокировки игры.
        /// </summary>
        public void Start()
        {
            // Публикуем начало определения аккаунта.
            State = AccountState.Resolving;
            Debug.Log("[Account] State: Resolving");

            // Запускаем незавершённую пока логику гостя без ожидания.
            _ = ResolveGuestAsync();
        }

        /// <summary>
        /// Наблюдает исключения фонового определения гостя.
        /// </summary>
        private async Task ResolveGuestAsync()
        {
            try
            {
                await Task.CompletedTask;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
