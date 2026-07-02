using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    internal interface IUnityPlayerAccountGateway
    {
        Task<string> SignInAndGetAccessTokenAsync();
    }
}
