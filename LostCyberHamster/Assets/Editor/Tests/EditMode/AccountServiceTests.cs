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
            var service = new AccountService(gateway);

            service.Start();

            Assert.AreEqual(expectedCreateAccount, gateway.LastCreateAccount);
            Assert.AreEqual(AccountState.Guest, service.State);
        }

        [Test]
        public void Start_WhenRestoreFails_DoesNotCreateGuest()
        {
            var gateway = new FakeAccountAuthenticationGateway
            {
                SessionTokenExists = true,
                SignInTask = Task.FromException(new InvalidOperationException("sign-in failed"))
            };
            var service = new AccountService(gateway);

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
            var service = new AccountService(gateway);

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
            var service = new AccountService(gateway);
            service.Start();

            service.ResetForTesting();
            pendingSignIn.SetResult(true);
            yield return null;

            Assert.AreEqual(2, gateway.ClearCredentialsCallCount);
            Assert.AreEqual(AccountState.NotStarted, service.State);
        }
    }
}
