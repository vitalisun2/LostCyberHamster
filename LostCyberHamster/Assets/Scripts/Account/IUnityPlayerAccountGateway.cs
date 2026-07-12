using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Определяет получение access token через пользовательский Unity Player Accounts flow.
    /// </summary>
    internal interface IUnityPlayerAccountGateway
    {
        /// <summary>
        /// Запускает пользовательский вход в Unity Player Accounts и возвращает выданный access token.
        /// </summary>
        Task<string> SignInAndGetAccessTokenAsync();
    }
}
