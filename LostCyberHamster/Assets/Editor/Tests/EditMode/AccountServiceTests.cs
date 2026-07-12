using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using NUnit.Framework;
using Unity.Services.Core;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет публичные сценарии AccountService через детерминированные gateway-заглушки без Unity Services и сети.
    /// </summary>
    public class AccountServiceTests
    {
        [Test]
        public void GivenNullAuthenticationGateway_WhenServiceIsCreated_ThenThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AccountService(null, new FakeUnityPlayerAccountGateway()));
        }

        [Test]
        public void GivenNullPlayerAccountGateway_WhenServiceIsCreated_ThenThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AccountService(new FakeUnityAuthenticationGateway(), null));
        }

        [TearDown]
        public void TearDown()
        {
            AccountServiceProvider.ResetForTests();
        }

        [Test]
        public async Task GivenNoSession_WhenEnsureSignedInIsCalled_ThenGuestSessionIsCreatedInOrder()
        {
            // Arrange
            var calls = new List<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = false,
                PlayerId = "guest-player"
            };
            var service = CreateService(auth, calls: calls);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            CollectionAssert.AreEqual(
                new[] { "Auth.Initialize", "Auth.SignInAnonymously", "Auth.IsUnityAccountLinked" },
                calls);
            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(1, auth.SignInAnonymouslyCalls);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, AccountState.Guest, "guest-player", true, false, string.Empty);
        }

        [TestCase(false, AccountState.Guest)]
        [TestCase(true, AccountState.Linked)]
        public async Task GivenCachedSession_WhenEnsureSignedInIsCalled_ThenAnonymousSignInIsSkipped(
            bool isLinked,
            AccountState expectedState)
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = isLinked,
                PlayerId = "cached-player"
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(0, auth.SignInAnonymouslyCalls);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, expectedState, "cached-player", true, isLinked, string.Empty);
        }

        [Test]
        public async Task GivenInitializationInvalidOperation_WhenEnsureSignedInIsCalled_ThenErrorIsPublished()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new InvalidOperationException("invalid configuration")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(0, auth.SignInAnonymouslyCalls);
            AssertSnapshot(snapshot, AccountState.Error, string.Empty, false, false, "invalid configuration");
        }

        [Test]
        public async Task GivenInitializationTimeout_WhenEnsureSignedInIsCalled_ThenOfflineIsPublished()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new TimeoutException("initialization timeout")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            AssertSnapshot(snapshot, AccountState.Offline, string.Empty, false, false, "initialization timeout");
        }

        [Test]
        public async Task GivenInitializationTransportFailure_WhenEnsureSignedInIsCalled_ThenOfflineIsPublished()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new RequestFailedException(CommonErrorCodes.TransportError, "transport failed")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            AssertSnapshot(snapshot, AccountState.Offline, string.Empty, false, false, "transport failed");
        }

        [Test]
        public async Task GivenInitializationBackendFailure_WhenEnsureSignedInIsCalled_ThenErrorIsPublished()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new RequestFailedException(42, "backend failed")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            AssertSnapshot(snapshot, AccountState.Error, string.Empty, false, false, "backend failed");
        }

        [Test]
        public async Task GivenAnonymousSignInInvalidOperation_WhenEnsureSignedInIsCalled_ThenErrorIsPublished()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                PlayerId = "stale-player",
                SignInAnonymouslyException = new InvalidOperationException("anonymous failed")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(1, auth.SignInAnonymouslyCalls);
            Assert.AreEqual(0, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, AccountState.Error, string.Empty, false, false, "anonymous failed");
        }

        [Test]
        public async Task GivenAnonymousSignInTimeout_WhenEnsureSignedInIsCalled_ThenOfflineIsPublished()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                SignInAnonymouslyException = new TimeoutException("anonymous timeout")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.EnsureSignedInAsync();

            // Assert
            AssertSnapshot(snapshot, AccountState.Offline, string.Empty, false, false, "anonymous timeout");
        }

        [Test]
        public async Task GivenUnsignedGateway_WhenRefreshIsCalled_ThenUnknownIsPublishedWithoutBackendCheck()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = false, PlayerId = "stale-player" };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.RefreshLinkStateAsync();

            // Assert
            Assert.AreEqual(0, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, AccountState.Unknown, string.Empty, false, false, string.Empty);
        }

        [TestCase(false, AccountState.Guest)]
        [TestCase(true, AccountState.Linked)]
        public async Task GivenSignedInGateway_WhenRefreshIsCalled_ThenCurrentLinkStateIsPublished(
            bool isLinked,
            AccountState expectedState)
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = isLinked,
                PlayerId = "player"
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.RefreshLinkStateAsync();

            // Assert
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, expectedState, "player", true, isLinked, string.Empty);
        }

        [Test]
        public async Task GivenPreviouslyLinkedSnapshot_WhenRefreshCheckFails_ThenIdentityIsKeptAndLinkFlagIsCleared()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player"
            };
            var service = CreateService(auth);
            await service.RefreshLinkStateAsync();
            auth.IsUnityAccountLinkedException = new InvalidOperationException("refresh failed");

            // Act
            AccountSnapshot snapshot = await service.RefreshLinkStateAsync();

            // Assert
            Assert.AreEqual(2, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, AccountState.Error, "linked-player", true, false, "refresh failed");
        }

        [Test]
        public async Task GivenSignedInGateway_WhenIsLinkedIsCalled_ThenOnlyLinkStateIsRefreshed()
        {
            // Arrange
            var calls = new List<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "player"
            };
            var service = CreateService(auth, calls: calls);

            // Act
            bool isLinked = await service.IsLinkedAsync();

            // Assert
            Assert.IsTrue(isLinked);
            CollectionAssert.AreEqual(new[] { "Auth.IsUnityAccountLinked" }, calls);
            Assert.AreEqual(0, auth.InitializeCalls);
            Assert.AreEqual(0, auth.SignInAnonymouslyCalls);
        }

        [Test]
        public async Task GivenUnsignedGateway_WhenIsLinkedIsCalled_ThenSessionIsEnsuredBeforeLinkCheck()
        {
            // Arrange
            var calls = new List<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = false,
                IsUnityAccountLinked = true,
                PlayerId = "player"
            };
            var service = CreateService(auth, calls: calls);

            // Act
            bool isLinked = await service.IsLinkedAsync();

            // Assert
            Assert.IsTrue(isLinked);
            CollectionAssert.AreEqual(
                new[] { "Auth.Initialize", "Auth.SignInAnonymously", "Auth.IsUnityAccountLinked" },
                calls);
        }

        [Test]
        public async Task GivenSessionEnsureFailure_WhenIsLinkedIsCalled_ThenFalseIsReturned()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new InvalidOperationException("offline")
            };
            var service = CreateService(auth);

            // Act
            bool isLinked = await service.IsLinkedAsync();

            // Assert
            Assert.IsFalse(isLinked);
            Assert.AreEqual(AccountState.Error, service.Snapshot.State);
            Assert.IsFalse(service.Snapshot.IsLinked);
        }

        [Test]
        public async Task GivenUnavailableSession_WhenInteractiveLinkIsCalled_ThenFailureIsReturnedWithoutPlayerAccountFlow()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                InitializeException = new InvalidOperationException("session unavailable")
            };
            var playerAccount = new FakeUnityPlayerAccountGateway();
            var service = CreateService(auth, playerAccount);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("session unavailable", result.ErrorMessage);
            Assert.AreEqual(0, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [Test]
        public async Task GivenAlreadyLinkedSession_WhenInteractiveLinkIsCalled_ThenSuccessIsReturnedWithoutPlayerAccountFlow()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player"
            };
            var playerAccount = new FakeUnityPlayerAccountGateway();
            var service = CreateService(auth, playerAccount);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual("linked-player", result.PlayerId);
            Assert.AreEqual(0, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [Test]
        public async Task GivenGuestSession_WhenInteractiveLinkSucceeds_ThenGatewaysAreCalledInOrderAndSnapshotIsLinked()
        {
            // Arrange
            var calls = new List<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            auth.LinkWithUnityHandler = token =>
            {
                auth.IsUnityAccountLinked = true;
                return Task.FromResult(AccountLinkResult.Success(auth.PlayerId));
            };
            var playerAccount = new FakeUnityPlayerAccountGateway(calls) { AccessToken = "unity-token" };
            var service = CreateService(auth, playerAccount);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            CollectionAssert.AreEqual(
                new[]
                {
                    "Auth.Initialize",
                    "Auth.IsUnityAccountLinked",
                    "PlayerAccount.SignIn",
                    "Auth.LinkWithUnity",
                    "Auth.IsUnityAccountLinked"
                },
                calls);
            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual("unity-token", auth.LastLinkAccessToken);
            Assert.AreEqual(1, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual(1, auth.LinkWithUnityCalls);
            AssertSnapshot(service.Snapshot, AccountState.Linked, "guest-player", true, true, string.Empty);
        }

        [Test]
        public async Task GivenPlayerAccountFlowException_WhenInteractiveLinkIsCalled_ThenFailedIsReturnedWithoutAuthLink()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            var playerAccount = new FakeUnityPlayerAccountGateway
            {
                SignInException = new InvalidOperationException("player account cancelled")
            };
            var service = CreateService(auth, playerAccount);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("player account cancelled", result.ErrorMessage);
            Assert.AreEqual(1, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [Test]
        public async Task GivenPlayerAccountFlowReturnsEmptyToken_WhenInteractiveLinkIsCalled_ThenFailedIsReturnedWithoutAuthLink()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            var playerAccount = new FakeUnityPlayerAccountGateway { AccessToken = " " };
            var service = CreateService(auth, playerAccount);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("Unity access token is empty.", result.ErrorMessage);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [TestCase(AccountLinkStatus.Unknown)]
        [TestCase(AccountLinkStatus.AlreadyLinked)]
        [TestCase(AccountLinkStatus.Failed)]
        public async Task GivenNonSuccessAuthResult_WhenInteractiveLinkIsCalled_ThenGuestIdentityIsKept(
            AccountLinkStatus linkStatus)
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "guest-player",
                LinkWithUnityHandler = _ => Task.FromResult(CreateLinkResult(linkStatus, "link rejected"))
            };
            var playerAccount = new FakeUnityPlayerAccountGateway { AccessToken = "unity-token" };
            var service = CreateService(auth, playerAccount);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(linkStatus, result.Status);
            Assert.AreEqual("guest-player", service.Snapshot.PlayerId);
            Assert.AreEqual(AccountState.Guest, service.Snapshot.State);
            Assert.IsFalse(service.Snapshot.IsLinked);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
        }

        [Test]
        public async Task GivenAuthLinkException_WhenInteractiveLinkIsCalled_ThenFailedIsReturned()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                LinkWithUnityException = new InvalidOperationException("auth link failed")
            };
            var service = CreateService(auth, new FakeUnityPlayerAccountGateway { AccessToken = "token" });

            // Act
            AccountLinkResult result = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("auth link failed", result.ErrorMessage);
            Assert.AreEqual(1, auth.LinkWithUnityCalls);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task GivenBlankAccessToken_WhenTokenLinkIsCalled_ThenFailedIsReturnedWithoutGatewayCall(string token)
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true };
            var service = CreateService(auth);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountWithAccessTokenAsync(token);

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("Unity access token is empty.", result.ErrorMessage);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
            Assert.AreEqual(0, auth.IsUnityAccountLinkedCalls);
        }

        [Test]
        public async Task GivenValidToken_WhenTokenLinkSucceeds_ThenLinkStateIsRefreshed()
        {
            // Arrange
            var calls = new List<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = true,
                PlayerId = "player"
            };
            auth.LinkWithUnityHandler = _ =>
            {
                auth.IsUnityAccountLinked = true;
                return Task.FromResult(AccountLinkResult.Success("player"));
            };
            var service = CreateService(auth, calls: calls);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            // Assert
            CollectionAssert.AreEqual(new[] { "Auth.LinkWithUnity", "Auth.IsUnityAccountLinked" }, calls);
            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            AssertSnapshot(service.Snapshot, AccountState.Linked, "player", true, true, string.Empty);
        }

        [Test]
        public async Task GivenSuccessfulAuthLinkAndRefreshFailure_WhenTokenLinkIsCalled_ThenSuccessAndErrorSnapshotAreReturned()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "player",
                LinkWithUnityHandler = _ => Task.FromResult(AccountLinkResult.Success("player")),
                IsUnityAccountLinkedException = new InvalidOperationException("refresh failed")
            };
            var service = CreateService(auth);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            // Assert
            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(service.Snapshot, AccountState.Error, "player", true, false, "refresh failed");
        }

        [Test]
        public async Task GivenUnsignedGateway_WhenTokenLinkIsCalled_ThenFailureIsReturnedWithoutGatewayCall()
        {
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = false };
            var service = CreateService(auth);

            AccountLinkResult result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("UGS player session is not signed in.", result.ErrorMessage);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);
        }

        [TestCase(AccountLinkStatus.Unknown)]
        [TestCase(AccountLinkStatus.AlreadyLinked)]
        [TestCase(AccountLinkStatus.Failed)]
        public async Task GivenNonSuccessAuthResult_WhenTokenLinkIsCalled_ThenSnapshotIsNotRefreshedOrSwitched(
            AccountLinkStatus linkStatus)
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            var service = CreateService(auth);
            await service.RefreshLinkStateAsync();
            auth.LinkWithUnityHandler = _ => Task.FromResult(CreateLinkResult(linkStatus, "link rejected"));

            // Act
            AccountLinkResult result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            // Assert
            Assert.AreEqual(linkStatus, result.Status);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(service.Snapshot, AccountState.Guest, "guest-player", true, false, string.Empty);
        }

        [Test]
        public async Task GivenAuthLinkException_WhenTokenLinkIsCalled_ThenFailedIsReturnedWithoutRefresh()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                LinkWithUnityException = new InvalidOperationException("link exception")
            };
            var service = CreateService(auth);

            // Act
            AccountLinkResult result = await service.LinkUnityAccountWithAccessTokenAsync("token");

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("link exception", result.ErrorMessage);
            Assert.AreEqual(1, auth.LinkWithUnityCalls);
            Assert.AreEqual(0, auth.IsUnityAccountLinkedCalls);
        }

        [Test]
        public async Task GivenLinkedSession_WhenUnlinkSucceeds_ThenStateIsRefreshedAsGuestInOrder()
        {
            // Arrange
            var calls = new List<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "player"
            };
            var service = CreateService(auth, calls: calls);

            // Act
            AccountSnapshot snapshot = await service.UnlinkUnityAccountAsync();

            // Assert
            CollectionAssert.AreEqual(new[] { "Auth.UnlinkUnity", "Auth.IsUnityAccountLinked" }, calls);
            Assert.AreEqual(1, auth.UnlinkUnityCalls);
            AssertSnapshot(snapshot, AccountState.Guest, "player", true, false, string.Empty);
        }

        [Test]
        public async Task GivenPreviouslyLinkedSnapshot_WhenUnlinkThrows_ThenErrorClearsLinkFlagAndKeepsIdentity()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player"
            };
            var service = CreateService(auth);
            await service.RefreshLinkStateAsync();
            auth.UnlinkUnityException = new InvalidOperationException("unlink failed");

            // Act
            AccountSnapshot snapshot = await service.UnlinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(1, auth.UnlinkUnityCalls);
            AssertSnapshot(snapshot, AccountState.Error, "linked-player", true, false, "unlink failed");
        }

        [Test]
        public async Task GivenUnlinkSucceedsButRefreshFails_WhenUnlinkIsCalled_ThenErrorClearsLinkFlag()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                IsUnityAccountLinked = true,
                PlayerId = "linked-player",
                IsUnityAccountLinkedException = new InvalidOperationException("refresh failed")
            };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.UnlinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(1, auth.UnlinkUnityCalls);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, AccountState.Error, "linked-player", true, false, "refresh failed");
        }

        [Test]
        public async Task GivenUnsignedGateway_WhenUnlinkIsCalled_ThenNoOpPublishesUnknown()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = false, PlayerId = "stale-player" };
            var service = CreateService(auth);

            // Act
            AccountSnapshot snapshot = await service.UnlinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(0, auth.UnlinkUnityCalls);
            Assert.AreEqual(0, auth.IsUnityAccountLinkedCalls);
            AssertSnapshot(snapshot, AccountState.Unknown, string.Empty, false, false, string.Empty);
        }

        [Test]
        public async Task GivenOlderRefreshCompletesLast_WhenNewerRefreshPublished_ThenNewestSnapshotWins()
        {
            var firstRefresh = CreateCompletion<bool>();
            int attempt = 0;
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "player",
                IsUnityAccountLinkedHandler = () => attempt++ == 0
                    ? firstRefresh.Task
                    : Task.FromResult(true)
            };
            var service = CreateService(auth);
            Task<AccountSnapshot> older = service.RefreshLinkStateAsync();

            AccountSnapshot newer = await service.RefreshLinkStateAsync();
            firstRefresh.SetResult(false);
            AccountSnapshot olderResult = await older;

            AssertSnapshot(newer, AccountState.Linked, "player", true, true, string.Empty);
            AssertSnapshot(olderResult, AccountState.Linked, "player", true, true, string.Empty);
            AssertSnapshot(service.Snapshot, AccountState.Linked, "player", true, true, string.Empty);
        }

        [Test]
        public async Task GivenStateSubscriber_WhenSnapshotChanges_ThenPublishedSnapshotMatchesReturnedSnapshot()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true, PlayerId = "event-player" };
            var service = CreateService(auth);
            AccountSnapshot observed = default;
            int eventCalls = 0;
            service.StateChanged += snapshot =>
            {
                observed = snapshot;
                eventCalls++;
            };

            // Act
            AccountSnapshot returned = await service.RefreshLinkStateAsync();

            // Assert
            Assert.AreEqual(1, eventCalls);
            AssertSnapshot(observed, returned.State, returned.PlayerId, returned.IsSignedIn, returned.IsLinked, returned.ErrorMessage);
        }

        [Test]
        public async Task GivenThrowingStateSubscriber_WhenSnapshotChanges_ThenOtherSubscribersAndOperationContinue()
        {
            // Arrange
            var auth = new FakeUnityAuthenticationGateway { IsSignedIn = true, PlayerId = "event-player" };
            var service = CreateService(auth);
            AccountSnapshot observed = default;
            service.StateChanged += _ => throw new InvalidOperationException("subscriber failed");
            service.StateChanged += snapshot => observed = snapshot;

            // Act
            AccountSnapshot returned = await service.RefreshLinkStateAsync();

            // Assert
            Assert.AreEqual(AccountState.Guest, returned.State);
            Assert.AreEqual(returned.PlayerId, observed.PlayerId);
            Assert.AreEqual(returned.State, observed.State);
        }

        [Test]
        public void GivenProviderOverride_WhenResetIsCalled_ThenDefaultSingletonIsRestored()
        {
            // Arrange
            IAccountService defaultService = AccountServiceProvider.Current;
            var overrideService = new StubAccountService();
            AccountServiceProvider.SetForTests(overrideService);
            Assert.AreSame(overrideService, AccountServiceProvider.Current);

            // Act
            AccountServiceProvider.ResetForTests();

            // Assert
            Assert.AreSame(defaultService, AccountServiceProvider.Current);
        }

        [Test]
        public async Task GivenTwoConcurrentInteractiveLinks_WhenFirstIsPending_ThenOnlyOneOperationRuns()
        {
            // Arrange
            var calls = new List<string>();
            var tokenCompletion = CreateCompletion<string>();
            var auth = new FakeUnityAuthenticationGateway(calls)
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            auth.LinkWithUnityHandler = _ =>
            {
                auth.IsUnityAccountLinked = true;
                return Task.FromResult(AccountLinkResult.Success("guest-player"));
            };
            var playerAccount = new FakeUnityPlayerAccountGateway(calls)
            {
                SignInHandler = () => tokenCompletion.Task
            };
            var service = CreateService(auth, playerAccount);

            // Act
            Task<AccountLinkResult> first = service.LinkUnityAccountAsync();
            Task<AccountLinkResult> second = service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(1, auth.InitializeCalls);
            Assert.AreEqual(1, auth.IsUnityAccountLinkedCalls);
            Assert.AreEqual(1, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual(0, auth.LinkWithUnityCalls);

            tokenCompletion.SetResult("token");
            AccountLinkResult[] results = await Task.WhenAll(first, second);

            Assert.AreEqual(AccountLinkStatus.Success, results[0].Status);
            Assert.AreEqual(AccountLinkStatus.Success, results[1].Status);
            Assert.AreEqual(1, auth.LinkWithUnityCalls);
            Assert.AreEqual(2, auth.IsUnityAccountLinkedCalls);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Auth.Initialize",
                    "Auth.IsUnityAccountLinked",
                    "PlayerAccount.SignIn",
                    "Auth.LinkWithUnity",
                    "Auth.IsUnityAccountLinked"
                },
                calls);
        }

        [Test]
        public async Task GivenSharedInteractiveLinkFailure_WhenNextLinkIsCalled_ThenAFreshOperationRetries()
        {
            // Arrange
            var firstTokenCompletion = CreateCompletion<string>();
            int tokenAttempt = 0;
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            auth.LinkWithUnityHandler = _ =>
            {
                auth.IsUnityAccountLinked = true;
                return Task.FromResult(AccountLinkResult.Success("guest-player"));
            };
            var playerAccount = new FakeUnityPlayerAccountGateway
            {
                SignInHandler = () => tokenAttempt++ == 0
                    ? firstTokenCompletion.Task
                    : Task.FromResult("retry-token")
            };
            var service = CreateService(auth, playerAccount);
            Task<AccountLinkResult> first = service.LinkUnityAccountAsync();
            Task<AccountLinkResult> shared = service.LinkUnityAccountAsync();
            firstTokenCompletion.SetException(new InvalidOperationException("first attempt failed"));
            AccountLinkResult[] failedResults = await Task.WhenAll(first, shared);

            // Act
            AccountLinkResult retryResult = await service.LinkUnityAccountAsync();

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, failedResults[0].Status);
            Assert.AreEqual(AccountLinkStatus.Failed, failedResults[1].Status);
            Assert.AreEqual(AccountLinkStatus.Success, retryResult.Status);
            Assert.AreEqual(2, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual(2, auth.InitializeCalls);
            Assert.AreEqual(1, auth.LinkWithUnityCalls);
            Assert.AreEqual("retry-token", auth.LastLinkAccessToken);
        }

        [Test]
        public async Task GivenCompletedInteractiveLink_WhenCalledAgain_ThenFreshOperationStartsImmediately()
        {
            int attempt = 0;
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            auth.LinkWithUnityHandler = _ =>
                Task.FromResult(AccountLinkResult.Success(auth.PlayerId));
            var playerAccount = new FakeUnityPlayerAccountGateway
            {
                SignInHandler = () => Task.FromResult($"token-{++attempt}")
            };
            var service = CreateService(auth, playerAccount);

            AccountLinkResult first = await service.LinkUnityAccountAsync();
            AccountLinkResult second = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Success, first.Status);
            Assert.AreEqual(AccountLinkStatus.Success, second.Status);
            Assert.AreEqual(2, playerAccount.SignInAndGetAccessTokenCalls);
            Assert.AreEqual("token-2", auth.LastLinkAccessToken);
        }

        [Test]
        public async Task GivenInteractiveTimeout_WhenCalledAgain_ThenFreshOperationStarts()
        {
            int attempt = 0;
            var auth = new FakeUnityAuthenticationGateway
            {
                IsSignedIn = true,
                PlayerId = "guest-player"
            };
            auth.LinkWithUnityHandler = _ =>
                Task.FromResult(AccountLinkResult.Success(auth.PlayerId));
            var playerAccount = new FakeUnityPlayerAccountGateway
            {
                SignInHandler = () => ++attempt == 1
                    ? Task.FromException<string>(new TimeoutException("oauth timeout"))
                    : Task.FromResult("retry-token")
            };
            var service = CreateService(auth, playerAccount);

            AccountLinkResult first = await service.LinkUnityAccountAsync();
            AccountLinkResult retry = await service.LinkUnityAccountAsync();

            Assert.AreEqual(AccountLinkStatus.Failed, first.Status);
            Assert.AreEqual(AccountLinkStatus.Success, retry.Status);
            Assert.AreEqual(2, playerAccount.SignInAndGetAccessTokenCalls);
        }

        private static AccountService CreateService(
            FakeUnityAuthenticationGateway auth,
            FakeUnityPlayerAccountGateway playerAccount = null,
            List<string> calls = null)
        {
            return new AccountService(
                auth,
                playerAccount ?? new FakeUnityPlayerAccountGateway(calls));
        }

        private static AccountLinkResult CreateLinkResult(AccountLinkStatus status, string message)
        {
            switch (status)
            {
                case AccountLinkStatus.Unknown:
                    return AccountLinkResult.Unknown(message);
                case AccountLinkStatus.AlreadyLinked:
                    return AccountLinkResult.AlreadyLinked(message);
                case AccountLinkStatus.Failed:
                    return AccountLinkResult.Failed(message);
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        private static TaskCompletionSource<T> CreateCompletion<T>()
        {
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static void AssertSnapshot(
            AccountSnapshot snapshot,
            AccountState state,
            string playerId,
            bool isSignedIn,
            bool isLinked,
            string errorMessage)
        {
            Assert.AreEqual(state, snapshot.State);
            Assert.AreEqual(playerId, snapshot.PlayerId);
            Assert.AreEqual(isSignedIn, snapshot.IsSignedIn);
            Assert.AreEqual(isLinked, snapshot.IsLinked);
            Assert.AreEqual(errorMessage, snapshot.ErrorMessage);
        }

        /// <summary>
        /// Имитирует Unity Authentication gateway и фиксирует вызовы для проверки оркестрации сервиса.
        /// </summary>
        private sealed class FakeUnityAuthenticationGateway : IUnityAuthenticationGateway
        {
            private readonly List<string> _calls;

            public FakeUnityAuthenticationGateway(List<string> calls = null)
            {
                _calls = calls;
            }

            public bool IsSignedIn { get; set; }
            public string PlayerId { get; set; } = "player";
            public bool IsUnityAccountLinked { get; set; }
            public Exception InitializeException { get; set; }
            public Exception SignInAnonymouslyException { get; set; }
            public Exception IsUnityAccountLinkedException { get; set; }
            public Func<Task<bool>> IsUnityAccountLinkedHandler { get; set; }
            public Exception LinkWithUnityException { get; set; }
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
                _calls?.Add("Auth.Initialize");
                if (InitializeException != null)
                {
                    throw InitializeException;
                }

                return Task.CompletedTask;
            }

            public Task SignInAnonymouslyAsync()
            {
                SignInAnonymouslyCalls++;
                _calls?.Add("Auth.SignInAnonymously");
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
                _calls?.Add("Auth.IsUnityAccountLinked");
                if (IsUnityAccountLinkedException != null)
                {
                    throw IsUnityAccountLinkedException;
                }

                return IsUnityAccountLinkedHandler != null
                    ? IsUnityAccountLinkedHandler()
                    : Task.FromResult(IsUnityAccountLinked);
            }

            public Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
            {
                LinkWithUnityCalls++;
                LastLinkAccessToken = accessToken;
                _calls?.Add("Auth.LinkWithUnity");
                if (LinkWithUnityException != null)
                {
                    throw LinkWithUnityException;
                }

                return LinkWithUnityHandler != null
                    ? LinkWithUnityHandler(accessToken)
                    : Task.FromResult(AccountLinkResult.Success(PlayerId));
            }

            public Task UnlinkUnityAsync()
            {
                UnlinkUnityCalls++;
                _calls?.Add("Auth.UnlinkUnity");
                if (UnlinkUnityException != null)
                {
                    throw UnlinkUnityException;
                }

                IsUnityAccountLinked = false;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Имитирует интерактивное получение Unity Player Account access token без SDK и сети.
        /// </summary>
        private sealed class FakeUnityPlayerAccountGateway : IUnityPlayerAccountGateway
        {
            private readonly List<string> _calls;

            public FakeUnityPlayerAccountGateway(List<string> calls = null)
            {
                _calls = calls;
            }

            public string AccessToken { get; set; } = "access-token";
            public Exception SignInException { get; set; }
            public Func<Task<string>> SignInHandler { get; set; }
            public int SignInAndGetAccessTokenCalls { get; private set; }

            public Task<string> SignInAndGetAccessTokenAsync()
            {
                SignInAndGetAccessTokenCalls++;
                _calls?.Add("PlayerAccount.SignIn");
                if (SignInException != null)
                {
                    throw SignInException;
                }

                return SignInHandler != null
                    ? SignInHandler()
                    : Task.FromResult(AccessToken);
            }
        }

        /// <summary>
        /// Представляет минимальную подмену IAccountService для проверки глобального provider override.
        /// </summary>
        private sealed class StubAccountService : IAccountService
        {
            public event Action<AccountSnapshot> StateChanged
            {
                add { }
                remove { }
            }

            public AccountSnapshot Snapshot => AccountSnapshot.Unknown;

            public Task<AccountSnapshot> EnsureSignedInAsync()
            {
                return Task.FromResult(Snapshot);
            }

            public Task<AccountSnapshot> RefreshLinkStateAsync()
            {
                return Task.FromResult(Snapshot);
            }

            public Task<bool> IsLinkedAsync()
            {
                return Task.FromResult(false);
            }

            public Task<AccountLinkResult> LinkUnityAccountAsync()
            {
                return Task.FromResult(AccountLinkResult.Unknown());
            }

            public Task<AccountLinkResult> LinkUnityAccountWithAccessTokenAsync(string accessToken)
            {
                return Task.FromResult(AccountLinkResult.Unknown());
            }

            public Task<AccountSnapshot> UnlinkUnityAccountAsync()
            {
                return Task.FromResult(Snapshot);
            }
        }
    }
}
