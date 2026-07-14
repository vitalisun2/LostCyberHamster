using System.Threading.Tasks;
using Assets.Scripts.Account;

namespace Assets.Tests.EditMode
{
    internal sealed class FakeUnityPlayerAccountGateway : IUnityPlayerAccountGateway
    {
        public Task<string> SignInTask { get; set; } = Task.FromResult("access-token");

        public Task<string> SignInAsync()
        {
            return SignInTask;
        }
    }
}
