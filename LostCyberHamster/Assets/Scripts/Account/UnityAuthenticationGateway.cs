using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    internal sealed class UnityAuthenticationGateway : IUnityAuthenticationGateway
    {
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => AuthenticationService.Instance.PlayerId ?? string.Empty;

        /// <summary>
        /// Инициализирует Unity Gaming Services для последующих вызовов Authentication SDK.
        /// </summary>
        public Task InitializeAsync()
        {
            return UnityServices.InitializeAsync();
        }

        /// <summary>
        /// Восстанавливает закэшированного UGS-игрока или создаёт новую гостевую учётную запись.
        /// </summary>
        public Task SignInAnonymouslyAsync()
        {
            return AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Проверяет Unity Player Account среди внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        public async Task<bool> IsUnityAccountLinkedAsync()
        {
            var playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();
            return !string.IsNullOrEmpty(playerInfo.GetUnityId());
        }

        /// <summary>
        /// Связывает Unity Player Account с текущим UGS-игроком и переводит ожидаемые ошибки SDK во внутренний результат.
        /// </summary>
        public async Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            try
            {
                await AuthenticationService.Instance.LinkWithUnityAsync(accessToken);
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
        /// Переключает текущую гостевую сессию на существующий Unity Player Account.
        /// </summary>
        public async Task<AccountLinkResult> SignInWithUnityAsync(string accessToken)
        {
            // UGS разрешает внешний sign-in только из состояния SignedOut.
            AuthenticationService.Instance.SignOut();

            try
            {
                await AuthenticationService.Instance.SignInWithUnityAsync(
                    accessToken,
                    new SignInOptions { CreateAccount = false });
                return AccountLinkResult.Success(PlayerId);
            }
            catch (AuthenticationException ex)
            {
                return await ResumeCachedPlayerOrFailAsync(ex.Message);
            }
            catch (RequestFailedException ex)
            {
                return await ResumeCachedPlayerOrFailAsync(ex.Message);
            }
        }

        /// <summary>
        /// Удаляет Unity Player Account из внешних идентификаторов текущего UGS-игрока.
        /// </summary>
        public Task UnlinkUnityAsync()
        {
            return AuthenticationService.Instance.UnlinkUnityAsync();
        }

        /// <summary>
        /// Возвращает сохранённую гостевую сессию после неудачного переключения аккаунта.
        /// </summary>
        private static async Task<AccountLinkResult> ResumeCachedPlayerOrFailAsync(string signInError)
        {
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                return AccountLinkResult.Failed(signInError);
            }
            catch (AuthenticationException ex)
            {
                return AccountLinkResult.Failed($"{signInError} Cached player recovery failed: {ex.Message}");
            }
            catch (RequestFailedException ex)
            {
                return AccountLinkResult.Failed($"{signInError} Cached player recovery failed: {ex.Message}");
            }
        }
    }
}
