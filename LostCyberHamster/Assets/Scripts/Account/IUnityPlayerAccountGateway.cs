using System.Threading.Tasks;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Запускает вход через Unity Player Accounts и возвращает access token.
    /// </summary>
    public interface IUnityPlayerAccountGateway
    {
        /// <summary>
        /// Открывает системный flow входа и возвращает полученный access token.
        /// </summary>
        Task<string> SignInAsync();
    }
}
