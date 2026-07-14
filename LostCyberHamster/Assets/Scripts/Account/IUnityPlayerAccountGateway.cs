using System.Threading.Tasks;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Предоставляет текущую сессию Unity Player Accounts или запускает browser flow входа.
    /// </summary>
    public interface IUnityPlayerAccountGateway
    {
        bool IsSignedIn { get; }

        /// <summary>
        /// Возвращает access token текущей сессии или открывает системный flow входа.
        /// </summary>
        Task<string> SignInAsync();

        /// <summary>
        /// Завершает текущую локальную сессию Unity Player Accounts.
        /// </summary>
        void SignOut();
    }
}
