using System;
using System.Collections;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Assets.Tests.EditMode
{
    public sealed class AccountServiceTests
    {
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Start_SelectsExpectedGuestScenario(bool sessionTokenExists, bool expectedCreateAccount)
        {
            var gateway = new FakeAccountAuthenticationGateway
            {
                SessionTokenExists = sessionTokenExists
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());

            service.Start();

            Assert.AreEqual(expectedCreateAccount, gateway.LastCreateAccount);
            Assert.AreEqual(AccountState.Guest, service.State);
        }

        [Test]
        public void Start_WithLinkedRestoredSession_SetsLinked()
        {
            var gateway = new FakeAccountAuthenticationGateway
            {
                SessionTokenExists = true,
                IsUnityPlayerAccountLinked = true
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());

            service.Start();

            Assert.AreEqual(false, gateway.LastCreateAccount);
            Assert.AreEqual(AccountState.Linked, service.State);
        }

        [Test]
        public void Start_WhenRestoreFails_DoesNotCreateGuest()
        {
            var gateway = new FakeAccountAuthenticationGateway
            {
                SessionTokenExists = true,
                SignInTask = Task.FromException(new InvalidOperationException("sign-in failed"))
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());

            var loggerWasEnabled = Debug.unityLogger.logEnabled;
            try
            {
                Debug.unityLogger.logEnabled = false;
                service.Start();
            }
            finally
            {
                Debug.unityLogger.logEnabled = loggerWasEnabled;
            }

            Assert.AreEqual(1, gateway.SignInCallCount);
            Assert.AreEqual(false, gateway.LastCreateAccount);
            Assert.AreEqual(AccountState.Error, service.State);
        }

        [Test]
        public void Start_WhenAlreadyResolving_DoesNotStartSecondSignIn()
        {
            var pendingSignIn = new TaskCompletionSource<bool>();
            var gateway = new FakeAccountAuthenticationGateway
            {
                SignInTask = pendingSignIn.Task
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());

            service.Start();
            service.Start();

            Assert.AreEqual(1, gateway.SignInCallCount);
            Assert.AreEqual(AccountState.Resolving, service.State);
        }

        [UnityTest]
        public IEnumerator ResetForTesting_WhenPreviousSignInCompletes_DoesNotRestoreStaleState()
        {
            var pendingSignIn = new TaskCompletionSource<bool>();
            var gateway = new FakeAccountAuthenticationGateway
            {
                SignInTask = pendingSignIn.Task
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());
            service.Start();

            service.ResetForTesting();
            pendingSignIn.SetResult(true);
            yield return null;

            Assert.AreEqual(2, gateway.ClearCredentialsCallCount);
            Assert.AreEqual(AccountState.NotStarted, service.State);
        }

        [Test]
        public async Task LinkCurrentGuestAsync_WhenSuccessful_PreservesPlayerIdAndSetsLinked()
        {
            var gateway = new FakeAccountAuthenticationGateway
            {
                PlayerId = "guest-player-id"
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());
            service.Start();

            var result = await service.LinkCurrentGuestAsync();

            Assert.AreEqual(AccountLinkResult.Linked, result);
            Assert.AreEqual("guest-player-id", gateway.PlayerId);
            Assert.AreEqual("access-token", gateway.LastAccessToken);
            Assert.AreEqual(1, gateway.LinkCallCount);
            Assert.AreEqual(AccountState.Linked, service.State);
        }

        [Test]
        public async Task LinkCurrentGuestAsync_WhenAccountAlreadyLinked_PreservesGuestIdentity()
        {
            var gateway = new FakeAccountAuthenticationGateway
            {
                PlayerId = "guest-player-id",
                LinkTask = Task.FromResult(AccountLinkResult.Conflict)
            };
            var service = new AccountService(gateway, new FakeUnityPlayerAccountGateway());
            service.Start();

            var result = await service.LinkCurrentGuestAsync();

            Assert.AreEqual(AccountLinkResult.Conflict, result);
            Assert.AreEqual(AccountState.Guest, service.State);
            Assert.AreEqual("guest-player-id", gateway.PlayerId);
            Assert.AreEqual(1, gateway.LinkCallCount);
        }
    }
}
