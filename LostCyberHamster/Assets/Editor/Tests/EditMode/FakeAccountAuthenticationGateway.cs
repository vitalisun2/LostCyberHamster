using System.Threading.Tasks;
using Assets.Scripts.Account;

namespace Assets.Tests.EditMode
{
    internal sealed class FakeAccountAuthenticationGateway : IAccountAuthenticationGateway
    {
        private bool _hasPreservedSession;
        private bool _preservedSessionIsUnityPlayerAccountLinked;
        private string _preservedPlayerId;

        public bool SessionTokenExists { get; set; }

        public bool IsUnityPlayerAccountLinked { get; set; }

        public bool IsSignedIn { get; set; }

        public string PlayerId { get; set; } = "guest-player-id";

        public string ExistingAccountPlayerId { get; set; } = "linked-player-id";

        public bool? LastCreateAccount { get; private set; }

        public int SignInCallCount { get; private set; }

        public int ClearCredentialsCallCount { get; private set; }

        public int PreserveCredentialsCallCount { get; private set; }

        public Task SignInTask { get; set; } = Task.CompletedTask;

        public Task SignInWithUnityTask { get; set; } = Task.CompletedTask;

        public Task<AccountLinkResult> LinkTask { get; set; } = Task.FromResult(AccountLinkResult.Linked);

        public string LastAccessToken { get; private set; }

        public int LinkCallCount { get; private set; }

        public async Task SignInAnonymouslyAsync(bool createAccount)
        {
            SignInCallCount++;
            LastCreateAccount = createAccount;
            await SignInTask;
            IsSignedIn = true;

            if (createAccount)
            {
                IsUnityPlayerAccountLinked = false;
            }
            else if (_hasPreservedSession)
            {
                IsUnityPlayerAccountLinked = _preservedSessionIsUnityPlayerAccountLinked;
                PlayerId = _preservedPlayerId;
            }
        }

        public Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            LinkCallCount++;
            LastAccessToken = accessToken;
            return LinkTask;
        }

        public async Task SignInWithUnityAsync(string accessToken)
        {
            await SignInWithUnityTask;
            IsSignedIn = true;
            IsUnityPlayerAccountLinked = true;
            PlayerId = ExistingAccountPlayerId;
        }

        public void SignOutPreservingCredentials()
        {
            PreserveCredentialsCallCount++;

            if (!_hasPreservedSession)
            {
                _hasPreservedSession = true;
                _preservedSessionIsUnityPlayerAccountLinked = IsUnityPlayerAccountLinked;
                _preservedPlayerId = PlayerId;
            }

            IsSignedIn = false;
        }

        public Task UnlinkUnityAsync()
        {
            return Task.CompletedTask;
        }

        public void SignOutAndClearLocalCredentials()
        {
            ClearCredentialsCallCount++;
            _hasPreservedSession = false;
            _preservedSessionIsUnityPlayerAccountLinked = false;
            _preservedPlayerId = null;
            IsSignedIn = false;
            IsUnityPlayerAccountLinked = false;
        }
    }
}
