using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Адаптирует Unity Authentication SDK к внутренним контрактам account-слоя.
    /// </summary>
    internal sealed class UnityAuthenticationGateway : IUnityAuthenticationGateway
    {
        private readonly IUnityAuthenticationSdk _sdk;

        internal UnityAuthenticationGateway()
            : this(new UnityAuthenticationSdk())
        {
        }

        internal UnityAuthenticationGateway(IUnityAuthenticationSdk sdk)
        {
            _sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        }

        public bool IsSignedIn => _sdk.IsSignedIn;

        public string PlayerId => _sdk.PlayerId ?? string.Empty;

        /// <summary>
        /// Инициализирует Unity Gaming Services для последующих вызовов Authentication SDK.
        /// </summary>
        public Task InitializeAsync()
        {
            return _sdk.InitializeAsync();
        }

        /// <summary>
        /// Восстанавливает закэшированного UGS-игрока или создаёт новую гостевую учётную запись.
        /// </summary>
        public Task SignInAnonymouslyAsync()
        {
            return _sdk.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Проверяет Unity Player Account среди внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        public async Task<bool> IsUnityAccountLinkedAsync()
        {
            string unityAccountId = await _sdk.GetUnityAccountIdAsync();
            return !string.IsNullOrEmpty(unityAccountId);
        }

        /// <summary>
        /// Связывает Unity Player Account с текущим UGS-игроком и переводит ожидаемые ошибки SDK во внутренний результат.
        /// </summary>
        public async Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            try
            {
                await _sdk.LinkWithUnityAsync(accessToken);
                return AccountLinkResult.Success(PlayerId);
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return AccountLinkResult.AlreadyLinked(ex.Message);
            }
            catch (AuthenticationException ex)
            {
                return AccountLinkResult.Failed(ex.Message);
            }
            catch (RequestFailedException ex)
            {
                return AccountLinkResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Удаляет Unity Player Account из внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        public Task UnlinkUnityAsync()
        {
            return _sdk.UnlinkUnityAsync();
        }
    }
}
