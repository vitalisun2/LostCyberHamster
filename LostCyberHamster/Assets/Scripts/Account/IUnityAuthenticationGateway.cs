using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    internal interface IUnityAuthenticationGateway
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }

        Task InitializeAsync();
        Task SignInAnonymouslyAsync();
        Task<bool> IsUnityAccountLinkedAsync();
        Task<AccountLinkResult> LinkWithUnityAsync(string accessToken);
        Task UnlinkUnityAsync();
    }
}
