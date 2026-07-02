using System;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class AccountServiceTests
    {
        [Test]
        public async Task EnsureSignedInAsync_WhenNotSignedIn_SignsInAnonymouslyAndReturnsGuest()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = false, PlayerId = "guest-player" };
            var service = CreateService(auth);

            var snapshot = await service.EnsureSignedInAsync();

            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(1, auth.SignInAnonymouslyCalls);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            Assert.AreEqual(AccountState.Guest, snapshot.State);
            Assert.IsTrue(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
            Assert.AreEqual("guest-player", snapshot.PlayerId);
        }

        [Test]
        public async Task EnsureSignedInAsync_WhenAlreadySignedIn_DoesNotSignInAnonymously()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true, PlayerId = "cached-player" };
            var service = CreateService(auth);

            var snapshot = await service.EnsureSignedInAsync();

            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(0, auth.SignInAnonymouslyCalls);
            Assert.AreEqual(AccountState.Guest, snapshot.State);
            Assert.AreEqual("cached-player", snapshot.PlayerId);
        }

        [Test]
        public async Task EnsureSignedInAsync_WhenAlreadyLinked_ReturnsLinked()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player"
            };
            var service = CreateService(auth);

            var snapshot = await service.EnsureSignedInAsync();

            Assert.AreEqual(AccountState.Linked, snapshot.State);
            Assert.IsTrue(snapshot.IsLinked);
            Assert.AreEqual("linked-player", snapshot.PlayerId);
        }

        [Test]
        public async Task EnsureSignedInAsync_WhenInitializeThrows_ReturnsOfflineSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new InvalidOperationException("init failed")
            };
            var service = CreateService(auth);

            var snapshot = await service.EnsureSignedInAsync();

            Assert.AreEqual(AccountState.Offline, snapshot.State);
            Assert.IsFalse(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
            Assert.AreEqual("init failed", snapshot.ErrorMessage);
        }

        [Test]
        public async Task EnsureSignedInAsync_WhenAnonymousSignInThrows_ReturnsOfflineSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = false,
                SignInAnonymouslyException = new InvalidOperationException("anonymous failed")
            };
            var service = CreateService(auth);

            var snapshot = await service.EnsureSignedInAsync();

            Assert.AreEqual(AccountState.Offline, snapshot.State);
            Assert.IsFalse(snapshot.IsSignedIn);
            Assert.AreEqual("anonymous failed", snapshot.ErrorMessage);
        }

        [Test]
        public async Task EnsureSignedInAsync_RaisesStateChangedOnSnapshotUpdate()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true, PlayerId = "event-player" };
            var service = CreateService(auth);
            AccountSnapshot observed = AccountSnapshot.Unknown;

            service.StateChanged += snapshot => observed = snapshot;
            var returned = await service.EnsureSignedInAsync();

            Assert.AreEqual(returned.State, observed.State);
            Assert.AreEqual(returned.PlayerId, observed.PlayerId);
        }

        [Test]
        public async Task RefreshLinkStateAsync_WhenNotSignedIn_ReturnsUnknown()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = false };
            var service = CreateService(auth);

            var snapshot = await service.RefreshLinkStateAsync();

            Assert.AreEqual(AccountState.Unknown, snapshot.State);
            Assert.IsFalse(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
            Assert.AreEqual(0, auth.IsUnityAccountLinkedCalls);
        }

        [Test]
        public async Task RefreshLinkStateAsync_WhenSignedInAndNotLinked_ReturnsGuest()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true, PlayerId = "guest-player" };
            var service = CreateService(auth);

            var snapshot = await service.RefreshLinkStateAsync();

            Assert.AreEqual(AccountState.Guest, snapshot.State);
            Assert.IsTrue(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
        }

        [Test]
        public async Task RefreshLinkStateAsync_WhenSignedInAndLinked_ReturnsLinked()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player"
            };
            var service = CreateService(auth);

            var snapshot = await service.RefreshLinkStateAsync();

            Assert.AreEqual(AccountState.Linked, snapshot.State);
            Assert.IsTrue(snapshot.IsLinked);
        }

        [Test]
        public async Task RefreshLinkStateAsync_WhenLinkStateCheckThrows_ReturnsErrorAndKeepsPreviousIdentity()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "linked-player",
                IsUnityAccountLinked = true
            };
            var service = CreateService(auth);
            await service.RefreshLinkStateAsync();
            auth.IsUnityAccountLinkedException = new InvalidOperationException("refresh failed");

            var snapshot = await service.RefreshLinkStateAsync();

            Assert.AreEqual(AccountState.Error, snapshot.State);
            Assert.AreEqual("linked-player", snapshot.PlayerId);
            Assert.IsTrue(snapshot.IsSignedIn);
            Assert.IsTrue(snapshot.IsLinked);
            Assert.AreEqual("refresh failed", snapshot.ErrorMessage);
        }

        [Test]
        public async Task IsLinkedAsync_WhenAlreadySignedIn_RefreshesState()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true
            };
            var service = CreateService(auth);

            var isLinked = await service.IsLinkedAsync();

            Assert.IsTrue(isLinked);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            Assert.AreEqual(0, auth.SignInAnonymouslyCalls);
        }

        [Test]
        public async Task IsLinkedAsync_WhenNotSignedIn_EnsuresSignInFirst()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = false,
                IsUnityAccountLinked = true,
                PlayerId = "player"
            };
            var service = CreateService(auth);

            var isLinked = await service.IsLinkedAsync();

            Assert.IsTrue(isLinked);
            Assert.AreEqual(1, auth.SignInAnonymouslyCalls);
        }

        [Test]
        public async Task IsLinkedAsync_WhenEnsureSignInFails_ReturnsFalse()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new InvalidOperationException("offline")
            };
            var service = CreateService(auth);

            var isLinked = await service.IsLinkedAsync();

            Assert.IsFalse(isLinked);
            Assert.AreEqual(AccountState.Offline, service.Snapshot.State);
        }

        [Test]
        public async Task LinkUnityAccountAsync_WhenOffline_ReturnsFailedAndDoesNotAskForPlayerAccountToken()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new InvalidOperationException("offline")
            };
            var playerAccount = new FakeUnityPlayerAccountGateway();
            var service = CreateService(auth, playerAccount);

            var result = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual(0, playerAccount.SignInAndGetAccessTokenCalls);
        }

        [Test]
        public async Task LinkUnityAccountAsync_WhenAlreadyLinked_ReturnsSuccessAndDoesNotAskForPlayerAccountToken()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player"
            };
            var playerAccount = new FakeUnityPlayerAccountGateway();
            var service = CreateService(auth, playerAccount);

            var result = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual("linked-player", result.PlayerId);
            Assert.AreEqual(0, playerAccount.SignInAndGetAccessTokenCalls);
        }

        [Test]
        public async Task LinkUnityAccountAsync_WhenGuestAndTokenReceived_LinksAccountAndRefreshesSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            auth.LinkWithUnityHandler = token =>
            {
                auth.IsUnityAccountLinked = true;
                return Task.FromResult(AccountLinkResult.Success(auth.PlayerId));
            };
            var playerAccount = new FakeUnityPlayerAccountGateway { AccessToken = "unity-token" };
            var service = CreateService(auth, playerAccount);

            var result = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual("unity-token", auth.LastLinkAccessToken);
            Assert.AreEqual(AccountState.Linked, service.Snapshot.State);
            Assert.IsTrue(service.Snapshot.IsLinked);
        }

        [Test]
        public async Task LinkUnityAccountAsync_WhenTokenGatewayThrows_ReturnsFailed()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            var playerAccount = new FakeUnityPlayerAccountGateway
            {
                SignInException = new InvalidOperationException("player account cancelled")
            };
            var service = CreateService(auth, playerAccount);

            var result = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("player account cancelled", result.ErrorMessage);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [Test]
        public async Task LinkUnityAccountAsync_WhenBackendReturnsAlreadyLinked_ReturnsAlreadyLinkedAndDoesNotMarkSnapshotLinked()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            auth.LinkWithUnityHandler = _ => Task.FromResult(AccountLinkResult.AlreadyLinked("already linked"));
            var playerAccount = new FakeUnityPlayerAccountGateway { AccessToken = "unity-token" };
            var service = CreateService(auth, playerAccount);

            var result = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.AlreadyLinked, result.Status);
            Assert.AreEqual(AccountState.Guest, service.Snapshot.State);
            Assert.IsFalse(service.Snapshot.IsLinked);
        }

        [Test]
        public async Task LinkUnityAccountAsync_WhenBackendReturnsFailed_ReturnsFailedAndKeepsGuestSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            auth.LinkWithUnityHandler = _ => Task.FromResult(AccountLinkResult.Failed("backend failed"));
            var playerAccount = new FakeUnityPlayerAccountGateway { AccessToken = "unity-token" };
            var service = CreateService(auth, playerAccount);

            var result = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("backend failed", result.ErrorMessage);
            Assert.AreEqual(AccountState.Guest, service.Snapshot.State);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task LinkUnityAccountWithAccessTokenAsync_WhenTokenIsNullOrEmpty_ReturnsFailed(string token)
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            var service = CreateService(auth);

            var result = await service.LinkUnityAccountWithAccessTokenAsync(token);

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [Test]
        public async Task LinkUnityAccountWithAccessTokenAsync_WhenLinkSucceeds_RefreshesSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true, PlayerId = "player" };
            auth.LinkWithUnityHandler = _ =>
            {
                auth.IsUnityAccountLinked = true;
                return Task.FromResult(AccountLinkResult.Success("player"));
            };
            var service = CreateService(auth);

            var result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual(AccountState.Linked, service.Snapshot.State);
        }

        [Test]
        public async Task LinkUnityAccountWithAccessTokenAsync_WhenAlreadyLinked_ReturnsAlreadyLinked()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            auth.LinkWithUnityHandler = _ => Task.FromResult(AccountLinkResult.AlreadyLinked("conflict"));
            var service = CreateService(auth);

            var result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            Assert.AreEqual(AccountLinkStatus.AlreadyLinked, result.Status);
            Assert.AreEqual("conflict", result.ErrorMessage);
        }

        [Test]
        public async Task UnlinkUnityAccountAsync_WhenGatewaySucceeds_RefreshesSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true
            };
            var service = CreateService(auth);
            await service.RefreshLinkStateAsync();

            var snapshot = await service.UnlinkUnityAccountAsync();

            Assert.AreEqual(1, auth.UnlinkUnityCalls);
            Assert.AreEqual(AccountState.Guest, snapshot.State);
            Assert.IsFalse(snapshot.IsLinked);
        }

        [Test]
        public async Task UnlinkUnityAccountAsync_WhenGatewayThrows_ReturnsErrorSnapshot()
        {
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                UnlinkUnityException = new InvalidOperationException("unlink failed")
            };
            var service = CreateService(auth);
            await service.RefreshLinkStateAsync();

            var snapshot = await service.UnlinkUnityAccountAsync();

            Assert.AreEqual(AccountState.Error, snapshot.State);
            Assert.AreEqual("unlink failed", snapshot.ErrorMessage);
            Assert.IsTrue(snapshot.IsLinked);
        }

        private static AccountService CreateService(
            FakeUnityAuthenticationGateway auth,
            FakeUnityPlayerAccountGateway playerAccount = null)
        {
            return new AccountService(auth, playerAccount ?? new FakeUnityPlayerAccountGateway());
        }

        private sealed class FakeUnityAuthenticationGateway : IUnityAuthenticationGateway
        {
            public bool IsSignedIn { get; set; }
            public string PlayerId { get; set; } = "player";
            public bool IsUnityAccountLinked { get; set; }
            public Exception InitializeException { get; set; }
            public Exception SignInAnonymouslyException { get; set; }
            public Exception IsUnityAccountLinkedException { get; set; }
            public Exception UnlinkUnityException { get; set; }
            public Func<string, Task<AccountLinkResult>> LinkWithUnityHandler { get; set; }
            public string LastLinkAccessToken { get; private set; }
            public int InitializeCalls { get; private set; }
            public int SignInAnonymouslyCalls { get; private set; }
            public int IsUnityAccountLinkedCalls { get; private set; }
            public int LinkWithUnityCalls { get; private set; }
            public int UnlinkUnityCalls { get; private set; }

            public Task InitializeAsync()
            {
                InitializeCalls++;
                if (InitializeException != null)
                {
                    throw InitializeException;
                }

                return Task.CompletedTask;
            }

            public Task SignInAnonymouslyAsync()
            {
                SignInAnonymouslyCalls++;
                if (SignInAnonymouslyException != null)
                {
                    throw SignInAnonymouslyException;
                }

                IsSignedIn = true;
                return Task.CompletedTask;
            }

            public Task<bool> IsUnityAccountLinkedAsync()
            {
                IsUnityAccountLinkedCalls++;
                if (IsUnityAccountLinkedException != null)
                {
                    throw IsUnityAccountLinkedException;
                }

                return Task.FromResult(IsUnityAccountLinked);
            }

            public Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
            {
                LinkWithUnityCalls++;
                LastLinkAccessToken = accessToken;
                return LinkWithUnityHandler != null
                    ? LinkWithUnityHandler(accessToken)
                    : Task.FromResult(AccountLinkResult.Success(PlayerId));
            }

            public Task UnlinkUnityAsync()
            {
                UnlinkUnityCalls++;
                if (UnlinkUnityException != null)
                {
                    throw UnlinkUnityException;
                }

                IsUnityAccountLinked = false;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeUnityPlayerAccountGateway : IUnityPlayerAccountGateway
        {
            public string AccessToken { get; set; } = "access-token";
            public Exception SignInException { get; set; }
            public int SignInAndGetAccessTokenCalls { get; private set; }

            public Task<string> SignInAndGetAccessTokenAsync()
            {
                SignInAndGetAccessTokenCalls++;
                if (SignInException != null)
                {
                    throw SignInException;
                }

                return Task.FromResult(AccessToken);
            }
        }
    }
}
