using System;
using System.Threading.Tasks;
using Assets.Scripts.DevTools.Account;
using Assets.Scripts.DevTools.Core;
using LostCyberHamster.Account;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет чистые back-stack и confirmation state-модели DEV-интерфейса.
    /// </summary>
    public sealed class DevToolsStateTests
    {
        [Test]
        public void Navigation_GivenResetRoot_WhenBackIsRequested_ThenReportsRootBoundary()
        {
            var navigation = new DevToolsNavigation<AccountDevToolsPage>();
            navigation.Reset(AccountDevToolsPage.Account);

            bool moved = navigation.TryGoBack(out AccountDevToolsPage page);

            Assert.IsFalse(moved);
            Assert.AreEqual(AccountDevToolsPage.Account, page);
        }

        [Test]
        public void Navigation_GivenHelpDetailPath_WhenBackIsRequested_ThenReturnsStrictlyToHelpAndAccount()
        {
            var navigation = new DevToolsNavigation<AccountDevToolsPage>();
            navigation.Reset(AccountDevToolsPage.Account);
            navigation.NavigateTo(AccountDevToolsPage.HelpIndex);
            navigation.NavigateTo(AccountDevToolsPage.HelpDetail);

            Assert.IsTrue(navigation.TryGoBack(out AccountDevToolsPage help));
            Assert.AreEqual(AccountDevToolsPage.HelpIndex, help);
            Assert.IsTrue(navigation.TryGoBack(out AccountDevToolsPage account));
            Assert.AreEqual(AccountDevToolsPage.Account, account);
            Assert.IsFalse(navigation.TryGoBack(out _));
        }

        [Test]
        public void Navigation_GivenSamePageTwice_WhenBackIsRequested_ThenDoesNotCreateDuplicateHistory()
        {
            var navigation = new DevToolsNavigation<AccountDevToolsPage>();
            navigation.Reset(AccountDevToolsPage.Account);
            navigation.NavigateTo(AccountDevToolsPage.Diagnostics);
            navigation.NavigateTo(AccountDevToolsPage.Diagnostics);

            Assert.IsTrue(navigation.TryGoBack(out AccountDevToolsPage page));
            Assert.AreEqual(AccountDevToolsPage.Account, page);
            Assert.IsFalse(navigation.TryGoBack(out _));
        }

        [Test]
        public void Confirmation_GivenPendingAction_WhenConsumed_ThenReturnsActionExactlyOnce()
        {
            var state = new AccountDevToolsConfirmationState();
            Func<Task> expected = () => Task.CompletedTask;
            state.Request("warning", expected);

            bool first = state.TryConsume(out Func<Task> actual);
            bool second = state.TryConsume(out Func<Task> repeated);

            Assert.IsTrue(first);
            Assert.AreSame(expected, actual);
            Assert.IsFalse(second);
            Assert.IsNull(repeated);
            Assert.IsFalse(state.HasPending);
            Assert.AreEqual(string.Empty, state.Warning);
        }

        [Test]
        public void Confirmation_GivenPendingAction_WhenCancelled_ThenClearsWarningAndAction()
        {
            var state = new AccountDevToolsConfirmationState();
            state.Request("warning", () => Task.CompletedTask);

            state.Cancel();

            Assert.IsFalse(state.HasPending);
            Assert.AreEqual(string.Empty, state.Warning);
            Assert.IsFalse(state.TryConsume(out _));
        }

        [Test]
        public void Confirmation_GivenNullAction_WhenRequested_ThenThrows()
        {
            var state = new AccountDevToolsConfirmationState();

            Assert.Throws<ArgumentNullException>(() => state.Request("warning", null));
            Assert.IsFalse(state.HasPending);
            Assert.AreEqual(string.Empty, state.Warning);
        }

        [Test]
        public async Task Controller_GivenPendingRefresh_WhenCalledTwice_ThenBusyAndResultTransitionsArePublished()
        {
            var refresh = new TaskCompletionSource<AccountSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            var account = new StubAccountService { RefreshHandler = () => refresh.Task };
            var controller = new AccountDevToolsController(new AccountDevToolsService(account));
            int presentationChanges = 0;
            controller.PresentationChanged += () => presentationChanges++;

            Task first = controller.RefreshAsync();
            Task ignored = controller.RefreshAsync();
            AccountDevToolsViewState busy = controller.GetViewState();

            Assert.IsTrue(busy.IsBusy);
            Assert.IsTrue(busy.HasResult);
            Assert.AreEqual(1, account.RefreshCalls);
            Assert.IsTrue(ignored.IsCompleted);

            var updated = new AccountSnapshot(AccountState.Guest, "player", true, false, string.Empty);
            account.SnapshotValue = updated;
            refresh.SetResult(updated);
            await first;
            AccountDevToolsViewState completed = controller.GetViewState();

            Assert.IsFalse(completed.IsBusy);
            StringAssert.Contains("Статус обновлён: Guest", completed.LastResult);
            Assert.AreEqual(2, presentationChanges);
        }

        private sealed class StubAccountService : IAccountService
        {
            public Func<Task<AccountSnapshot>> RefreshHandler { get; set; }
            public AccountSnapshot SnapshotValue { get; set; } = AccountSnapshot.Unknown;
            public int RefreshCalls { get; private set; }
            public event Action<AccountSnapshot> StateChanged { add { } remove { } }
            public AccountSnapshot Snapshot => SnapshotValue;
            public Task<AccountSnapshot> EnsureSignedInAsync() => Task.FromResult(SnapshotValue);

            public Task<AccountSnapshot> RefreshLinkStateAsync()
            {
                RefreshCalls++;
                return RefreshHandler != null ? RefreshHandler() : Task.FromResult(SnapshotValue);
            }

            public Task<bool> IsLinkedAsync() => Task.FromResult(SnapshotValue.IsLinked);
            public Task<AccountLinkResult> LinkUnityAccountAsync() => Task.FromResult(AccountLinkResult.Unknown());
            public Task<AccountLinkResult> LinkUnityAccountWithAccessTokenAsync(string accessToken) =>
                Task.FromResult(AccountLinkResult.Unknown());
            public Task<AccountSnapshot> UnlinkUnityAccountAsync() => Task.FromResult(SnapshotValue);
        }
    }
}
