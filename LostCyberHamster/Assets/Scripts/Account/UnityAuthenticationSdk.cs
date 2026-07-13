using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Адаптирует static Unity Authentication и Unity Services API к заменяемому SDK-порту.
    /// </summary>
    internal sealed class UnityAuthenticationSdk : IUnityAuthenticationSdk
    {
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => AuthenticationService.Instance.PlayerId;

        /// <summary>
        /// Инициализирует Unity Gaming Services.
        /// </summary>
        public Task InitializeAsync()
        {
            return UnityServices.InitializeAsync();
        }

        /// <summary>
        /// Восстанавливает закэшированного UGS-игрока или создаёт гостевого.
        /// </summary>
        public Task SignInAnonymouslyAsync()
        {
            return AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Возвращает Unity Player Account ID текущего UGS-игрока или пустую строку.
        /// </summary>
        public async Task<string> GetUnityAccountIdAsync()
        {
            PlayerInfo playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();
            return playerInfo?.GetUnityId() ?? string.Empty;
        }

        /// <summary>
        /// Связывает Unity Player Account с текущим UGS-игроком по access token.
        /// </summary>
        public Task LinkWithUnityAsync(string accessToken)
        {
            return AuthenticationService.Instance.LinkWithUnityAsync(accessToken);
        }

        /// <summary>
        /// Удаляет Unity Player Account из внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        public Task UnlinkUnityAsync()
        {
            return AuthenticationService.Instance.UnlinkUnityAsync();
        }
    }
}
