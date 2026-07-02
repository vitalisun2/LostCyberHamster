using System;
using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    public interface IAccountService
    {
        event Action<AccountSnapshot> StateChanged;

        AccountSnapshot Snapshot { get; }

        Task<AccountSnapshot> EnsureSignedInAsync();
        Task<AccountSnapshot> RefreshLinkStateAsync();
        Task<bool> IsLinkedAsync();
        Task<AccountLinkResult> LinkUnityAccountAsync();
        Task<AccountLinkResult> LinkUnityAccountWithAccessTokenAsync(string accessToken);
        Task<AccountSnapshot> UnlinkUnityAccountAsync();
    }
}
