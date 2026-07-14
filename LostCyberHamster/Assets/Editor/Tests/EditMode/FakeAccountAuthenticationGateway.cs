using System.Threading.Tasks;
using Assets.Scripts.Account;

namespace Assets.Tests.EditMode
{
    internal sealed class FakeAccountAuthenticationGateway : IAccountAuthenticationGateway
    {
        public bool SessionTokenExists { get; set; }

        public bool IsUnityPlayerAccountLinked { get; set; }

        public bool IsSignedIn { get; set; }

        public string PlayerId { get; set; } = "guest-player-id";

        public bool? LastCreateAccount { get; private set; }

        public int SignInCallCount { get; private set; }

        public int ClearCredentialsCallCount { get; private set; }

        public Task SignInTask { get; set; } = Task.CompletedTask;

        public Task<AccountLinkResult> LinkTask { get; set; } = Task.FromResult(AccountLinkResult.Linked);

        public string LastAccessToken { get; private set; }

        public int LinkCallCount { get; private set; }

        public Task SignInAnonymouslyAsync(bool createAccount)
        {
            SignInCallCount++;
            LastCreateAccount = createAccount;
            return SignInTask;
        }

        public Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            LinkCallCount++;
            LastAccessToken = accessToken;
            return LinkTask;
        }

        public Task SignInWithUnityAsync(string accessToken)
        {
            return Task.CompletedTask;
        }

        public Task UnlinkUnityAsync()
        {
            return Task.CompletedTask;
        }

        public void SignOutAndClearLocalCredentials()
        {
            ClearCredentialsCallCount++;
        }
    }
}
