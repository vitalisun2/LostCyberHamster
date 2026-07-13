using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Определяет минимальную заменяемую границу со static Unity Authentication SDK.
    /// </summary>
    internal interface IUnityAuthenticationSdk
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }

        /// <summary>
        /// Инициализирует Unity Gaming Services.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Восстанавливает закэшированного UGS-игрока или создаёт гостевого.
        /// </summary>
        Task SignInAnonymouslyAsync();

        /// <summary>
        /// Возвращает Unity Player Account ID текущего UGS-игрока или пустую строку.
        /// </summary>
        Task<string> GetUnityAccountIdAsync();

        /// <summary>
        /// Связывает Unity Player Account с текущим UGS-игроком по access token.
        /// </summary>
        Task LinkWithUnityAsync(string accessToken);

        /// <summary>
        /// Удаляет Unity Player Account из внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        Task UnlinkUnityAsync();
    }
}
