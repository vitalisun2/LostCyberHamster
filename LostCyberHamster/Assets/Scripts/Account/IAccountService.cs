using System;
using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Определяет сценарии управления UGS-сессией и связью с Unity Player Account.
    /// </summary>
    public interface IAccountService
    {
        event Action<AccountSnapshot> StateChanged;

        AccountSnapshot Snapshot { get; }

        /// <summary>
        /// Восстанавливает сохранённую UGS-сессию или создаёт гостевую и возвращает актуальное состояние аккаунта.
        /// </summary>
        Task<AccountSnapshot> EnsureSignedInAsync();

        /// <summary>
        /// Повторно запрашивает связанные идентификаторы текущего UGS-игрока и обновляет состояние аккаунта.
        /// </summary>
        Task<AccountSnapshot> RefreshLinkStateAsync();

        /// <summary>
        /// Гарантирует наличие UGS-сессии и проверяет, связан ли игрок с Unity Player Account.
        /// </summary>
        Task<bool> IsLinkedAsync();

        /// <summary>
        /// Запускает вход в Unity Player Account и связывает его с текущим UGS-игроком без смены Player ID при конфликте.
        /// </summary>
        Task<AccountLinkResult> LinkUnityAccountAsync();

        /// <summary>
        /// Связывает текущего UGS-игрока с Unity Player Account по access token без смены Player ID при конфликте.
        /// </summary>
        Task<AccountLinkResult> LinkUnityAccountWithAccessTokenAsync(string accessToken);

        /// <summary>
        /// Отвязывает Unity Player Account от текущего UGS-игрока и обновляет состояние аккаунта.
        /// </summary>
        Task<AccountSnapshot> UnlinkUnityAccountAsync();
    }
}
