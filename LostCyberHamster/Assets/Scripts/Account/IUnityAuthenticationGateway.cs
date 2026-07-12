using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    internal interface IUnityAuthenticationGateway
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }

        /// <summary>
        /// Инициализирует Unity Gaming Services перед обращением к Authentication SDK.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Восстанавливает закэшированного UGS-игрока или создаёт новую гостевую учётную запись.
        /// </summary>
        Task SignInAnonymouslyAsync();

        /// <summary>
        /// Проверяет наличие Unity Player Account среди внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        Task<bool> IsUnityAccountLinkedAsync();

        /// <summary>
        /// Связывает Unity Player Account с текущим UGS-игроком и преобразует ошибки SDK в результат операции.
        /// </summary>
        Task<AccountLinkResult> LinkWithUnityAsync(string accessToken);

        /// <summary>
        /// Переключает UGS-сессию на игрока, которому уже принадлежит Unity Player Account.
        /// </summary>
        Task<AccountLinkResult> SignInWithUnityAsync(string accessToken);

        /// <summary>
        /// Удаляет Unity Player Account из внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        Task UnlinkUnityAsync();
    }
}
