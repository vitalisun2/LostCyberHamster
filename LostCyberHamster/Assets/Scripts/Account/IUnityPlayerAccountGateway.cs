using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    internal interface IUnityPlayerAccountGateway
    {
        /// <summary>
        /// Запускает пользовательский вход в Unity Player Accounts и возвращает выданный access token.
        /// </summary>
        Task<string> SignInAndGetAccessTokenAsync();
    }
}
