using System;
using System.Threading.Tasks;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Определяет минимальный порт к Unity Player Accounts SDK без прямой зависимости от static service locator.
    /// </summary>
    internal interface IUnityPlayerAccountSdk
    {
        event Action SignedIn;
        event Action<RequestFailedException> SignInFailed;

        bool IsSignedIn { get; }
        string AccessToken { get; }

        /// <summary>
        /// Запускает интерактивный Unity Player Accounts flow.
        /// </summary>
        Task StartSignInAsync();

        /// <summary>
        /// Завершает локальную Unity Player Accounts OAuth-сессию.
        /// </summary>
        void SignOut();
    }
}
