using System.Threading.Tasks;
using Assets.Scripts.Account;

namespace Assets.Tests.EditMode
{
    internal sealed class FakeAccountAuthenticationGateway : IAccountAuthenticationGateway
    {
        public bool SessionTokenExists { get; set; }

        public bool? LastCreateAccount { get; private set; }

        public int SignInCallCount { get; private set; }

        public int ClearCredentialsCallCount { get; private set; }

        public Task SignInTask { get; set; } = Task.CompletedTask;

        public Task SignInAnonymouslyAsync(bool createAccount)
        {
            SignInCallCount++;
            LastCreateAccount = createAccount;
            return SignInTask;
        }

        public void SignOutAndClearLocalCredentials()
        {
            ClearCredentialsCallCount++;
        }
    }
}
