using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Адаптирует static PlayerAccountService к внутреннему порту account-слоя.
    /// </summary>
    internal sealed class UnityPlayerAccountSdk : IUnityPlayerAccountSdk
    {
        public event Action SignedIn
        {
            add => PlayerAccountService.Instance.SignedIn += value;
            remove => PlayerAccountService.Instance.SignedIn -= value;
        }

        public event Action<RequestFailedException> SignInFailed
        {
            add => PlayerAccountService.Instance.SignInFailed += value;
            remove => PlayerAccountService.Instance.SignInFailed -= value;
        }

        public bool IsSignedIn => PlayerAccountService.Instance.IsSignedIn;
        public string AccessToken => PlayerAccountService.Instance.AccessToken;

        /// <summary>
        /// Передаёт SDK команду открыть интерактивный Unity Player Accounts flow.
        /// </summary>
        public Task StartSignInAsync()
        {
            return PlayerAccountService.Instance.StartSignInAsync();
        }

        /// <summary>
        /// Завершает локальную Unity Player Accounts OAuth-сессию.
        /// </summary>
        public void SignOut()
        {
            PlayerAccountService.Instance.SignOut();
        }
    }
}
