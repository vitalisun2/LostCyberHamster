#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading.Tasks;
using LostCyberHamster.Account;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Определяет тестируемую границу account DEV-инструментов с account-слоем и Unity SDK.
    /// </summary>
    internal interface IAccountDevToolsService
    {
        AccountSnapshot Snapshot { get; }
        bool IsLocallyReadyForPlayerAccounts { get; }

        /// <summary>
        /// Возвращает короткое пользовательское описание текущего account-state.
        /// </summary>
        string GetHumanStatusText();

        /// <summary>
        /// Возвращает диагностическое описание локальной готовности account flow.
        /// </summary>
        string GetReadinessText();

        /// <summary>
        /// Возвращает диагностическое описание текущих UGS и UPA-сессий.
        /// </summary>
        string GetSessionText();

        /// <summary>
        /// Создаёт или восстанавливает UGS-сессию.
        /// </summary>
        Task<AccountSnapshot> EnsureSessionAsync();

        /// <summary>
        /// Перечитывает linked-state текущего UGS-игрока.
        /// </summary>
        Task<AccountSnapshot> RefreshAsync();

        /// <summary>
        /// Запускает интерактивную привязку Unity Player Account.
        /// </summary>
        Task<AccountLinkResult> LinkAsync();

        /// <summary>
        /// Отвязывает Unity Player Account от текущего UGS-игрока.
        /// </summary>
        Task<AccountSnapshot> UnlinkAsync();

        /// <summary>
        /// Завершает UGS-сессию, сохраняя локальные credentials.
        /// </summary>
        Task<AccountSnapshot> SignOutUgsKeepingCredentialsAsync();

        /// <summary>
        /// Завершает локальную Unity Player Accounts OAuth-сессию.
        /// </summary>
        void SignOutPlayerAccount();

        /// <summary>
        /// Удаляет локальные UGS credentials.
        /// </summary>
        Task<AccountSnapshot> ClearCachedIdentityAsync();

        /// <summary>
        /// Открывает Unity Dashboard во внешнем браузере.
        /// </summary>
        void OpenDashboard();
    }
}
#endif
