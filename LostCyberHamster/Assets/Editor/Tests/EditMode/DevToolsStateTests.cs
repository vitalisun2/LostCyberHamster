using System;
using System.Threading.Tasks;
using Assets.Scripts.DevTools.Account;
using Assets.Scripts.DevTools.Core;
using LostCyberHamster.Account;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет чистые state-модели, справку и controller account DEV-инструментов без Unity Services и UI.
    /// </summary>
    [Category("AccountDevTools")]
    [Timeout(5000)]
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
        public void ViewState_GivenNullStrings_WhenCreated_ThenNormalizesValuesAndHasNoResult()
        {
            var state = new AccountDevToolsViewState(null, null, null, false, false, false);

            Assert.AreEqual(string.Empty, state.HumanStatus);
            Assert.AreEqual(string.Empty, state.Diagnostics);
            Assert.AreEqual(string.Empty, state.LastResult);
            Assert.IsFalse(state.HasResult);
        }

        [Test]
        public void ViewState_GivenBusyOrNonEmptyResult_WhenRead_ThenHasResultReflectsPresentation()
        {
            var busy = new AccountDevToolsViewState("", "", "", true, false, false);
            var completed = new AccountDevToolsViewState("", "", "Готово", false, false, false);
            var whitespace = new AccountDevToolsViewState("", "", "   ", false, false, false);

            Assert.IsTrue(busy.HasResult);
            Assert.IsTrue(completed.HasResult);
            Assert.IsFalse(whitespace.HasResult);
        }

        [Test]
        public void HelpContent_GivenIndex_WhenRead_ThenContainsExactlySixNonEmptySections()
        {
            Assert.AreEqual(6, AccountDevToolsHelpContent.SectionCount);

            for (int index = 0; index < AccountDevToolsHelpContent.SectionCount; index++)
            {
                Assert.IsNotEmpty(AccountDevToolsHelpContent.GetTitle(index));
                Assert.IsNotEmpty(AccountDevToolsHelpContent.GetText(index));
            }

            Assert.AreEqual("Подготовка Unity Editor", AccountDevToolsHelpContent.GetTitle(
                AccountDevToolsHelpContent.EditorSetupSectionIndex));
        }

        [TestCase(-1)]
        [TestCase(6)]
        public void HelpContent_GivenOutOfRangeIndex_WhenRead_ThenThrows(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AccountDevToolsHelpContent.GetTitle(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => AccountDevToolsHelpContent.GetText(index));
        }

        [Test]
        public void Controller_GivenNullService_WhenCreated_ThenThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new AccountDevToolsController(null));
        }

        [Test]
        public void Controller_GivenServicePresentation_WhenViewStateIsRead_ThenMapsStatusReadinessAndDiagnostics()
        {
            var service = new FakeAccountDevToolsService
            {
                SnapshotValue = new AccountSnapshot(AccountState.Linked, "player", true, true, string.Empty),
                IsLocallyReady = true,
                HumanStatus = "Аккаунт привязан",
                Readiness = "readiness-details",
                Session = "session-details"
            };
            var controller = new AccountDevToolsController(service);

            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual("Аккаунт привязан", state.HumanStatus);
            Assert.IsTrue(state.IsLinked);
            Assert.IsTrue(state.IsLocallyReady);
            StringAssert.Contains("readiness-details", state.Diagnostics);
            StringAssert.Contains("session-details", state.Diagnostics);
            StringAssert.Contains("ПОСЛЕДНЯЯ ОПЕРАЦИЯ\n—", state.Diagnostics);
            Assert.IsFalse(state.HasResult);
        }

        [Test]
        public async Task Controller_GivenPendingRefresh_WhenCalledTwice_ThenBusyReentryAndPlayerSignOutAreIgnored()
        {
            var refresh = new TaskCompletionSource<AccountSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new FakeAccountDevToolsService { RefreshHandler = () => refresh.Task };
            var controller = new AccountDevToolsController(service);
            int presentationChanges = 0;
            controller.PresentationChanged += () => presentationChanges++;

            Task first = controller.RefreshAsync();
            Task ignored = controller.RefreshAsync();
            controller.SignOutPlayerAccount();
            AccountDevToolsViewState busy = controller.GetViewState();

            Assert.IsTrue(busy.IsBusy);
            Assert.IsTrue(busy.HasResult);
            Assert.AreEqual(1, service.RefreshCalls);
            Assert.AreEqual(0, service.SignOutPlayerAccountCalls);
            Assert.IsTrue(ignored.IsCompleted);

            var updated = new AccountSnapshot(AccountState.Guest, "player", true, false, string.Empty);
            service.SnapshotValue = updated;
            refresh.SetResult(updated);
            await first;
            AccountDevToolsViewState completed = controller.GetViewState();

            Assert.IsFalse(completed.IsBusy);
            StringAssert.Contains("Статус обновлён: гостевая сессия", completed.LastResult);
            Assert.AreEqual(2, presentationChanges);
        }

        [TestCase(false, "Гостевая сессия готова")]
        [TestCase(true, "Сессия готова: аккаунт привязан")]
        public async Task Controller_GivenAvailableSession_WhenEnsureIsCalled_ThenShowsShortSuccess(
            bool linked,
            string expectedResult)
        {
            var snapshot = new AccountSnapshot(
                linked ? AccountState.Linked : AccountState.Guest,
                "private-player-id",
                true,
                linked,
                string.Empty);
            var service = new FakeAccountDevToolsService { EnsureResult = snapshot, SnapshotValue = snapshot };
            var controller = new AccountDevToolsController(service);

            await controller.EnsureSessionAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains(expectedResult, state.LastResult);
            StringAssert.DoesNotContain("private-player-id", state.LastResult);
            StringAssert.Contains("private-player-id", state.Diagnostics);
            Assert.AreEqual(1, service.EnsureCalls);
        }

        [Test]
        public async Task Controller_GivenUnavailableSession_WhenEnsureIsCalled_ThenHidesRawFailureFromMain()
        {
            var failure = new AccountSnapshot(AccountState.Error, "private-player-id", false, false, "raw SDK failure");
            var service = new FakeAccountDevToolsService { EnsureResult = failure, SnapshotValue = failure };
            var controller = new AccountDevToolsController(service);

            await controller.EnsureSessionAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("Сессию создать не удалось", state.LastResult);
            StringAssert.DoesNotContain("private-player-id", state.LastResult);
            StringAssert.DoesNotContain("raw SDK failure", state.LastResult);
            StringAssert.Contains("private-player-id", state.Diagnostics);
            StringAssert.Contains("raw SDK failure", state.Diagnostics);
        }

        [TestCase(false, "гостевая сессия")]
        [TestCase(true, "аккаунт привязан")]
        public async Task Controller_GivenCurrentLinkState_WhenRefreshIsCalled_ThenShowsShortSuccess(
            bool linked,
            string expectedResult)
        {
            var snapshot = new AccountSnapshot(
                linked ? AccountState.Linked : AccountState.Guest,
                "private-player-id",
                true,
                linked,
                string.Empty);
            var service = new FakeAccountDevToolsService { RefreshResult = snapshot, SnapshotValue = snapshot };
            var controller = new AccountDevToolsController(service);

            await controller.RefreshAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains(expectedResult, state.LastResult);
            StringAssert.DoesNotContain("private-player-id", state.LastResult);
            StringAssert.Contains("private-player-id", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenRefreshFailure_WhenRefreshIsCalled_ThenMovesRawFailureToDiagnostics()
        {
            var failure = new AccountSnapshot(AccountState.Error, "player", true, false, "raw refresh failure");
            var service = new FakeAccountDevToolsService { RefreshResult = failure, SnapshotValue = failure };
            var controller = new AccountDevToolsController(service);

            await controller.RefreshAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("Статус не обновлён", state.LastResult);
            StringAssert.DoesNotContain("raw refresh failure", state.LastResult);
            StringAssert.Contains("raw refresh failure", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenSuccessfulLink_WhenLinkIsCalled_ThenHidesPlayerIdFromMain()
        {
            var service = new FakeAccountDevToolsService
            {
                LinkResult = AccountLinkResult.Success("private-player-id")
            };
            var controller = new AccountDevToolsController(service);

            await controller.LinkAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual("Unity Player Account привязан.", state.LastResult);
            StringAssert.DoesNotContain("private-player-id", state.LastResult);
            StringAssert.Contains("private-player-id", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenFailedLink_WhenLinkIsCalled_ThenMovesRawFailureToDiagnostics()
        {
            var service = new FakeAccountDevToolsService
            {
                LinkResult = AccountLinkResult.Failed("raw link failure")
            };
            var controller = new AccountDevToolsController(service);

            await controller.LinkAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("Привязка не выполнена", state.LastResult);
            StringAssert.DoesNotContain("raw link failure", state.LastResult);
            StringAssert.Contains("raw link failure", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenAlreadyLinkedConflict_WhenLinkIsCalled_ThenReportsBlockedSwitch()
        {
            var service = new FakeAccountDevToolsService
            {
                LinkResult = AccountLinkResult.AlreadyLinked("raw conflict")
            };
            var controller = new AccountDevToolsController(service);

            await controller.LinkAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("Переключение заблокировано", state.LastResult);
            StringAssert.DoesNotContain("raw conflict", state.LastResult);
            StringAssert.Contains("raw conflict", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenSuccessfulUnlink_WhenUnlinkIsCalled_ThenShowsShortSuccess()
        {
            var guest = new AccountSnapshot(AccountState.Guest, "private-player-id", true, false, string.Empty);
            var service = new FakeAccountDevToolsService { UnlinkResult = guest, SnapshotValue = guest };
            var controller = new AccountDevToolsController(service);

            await controller.UnlinkAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual("Unity Player Account отвязан.", state.LastResult);
            StringAssert.DoesNotContain("private-player-id", state.LastResult);
            StringAssert.Contains("private-player-id", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenFailedUnlink_WhenUnlinkIsCalled_ThenMovesRawFailureToDiagnostics()
        {
            var failure = new AccountSnapshot(AccountState.Error, "player", true, false, "raw unlink failure");
            var service = new FakeAccountDevToolsService { UnlinkResult = failure, SnapshotValue = failure };
            var controller = new AccountDevToolsController(service);

            await controller.UnlinkAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("Отвязка не выполнена", state.LastResult);
            StringAssert.DoesNotContain("raw unlink failure", state.LastResult);
            StringAssert.Contains("raw unlink failure", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenUgsSession_WhenSignOutIsCalled_ThenForwardsAndShowsShortSuccess()
        {
            var service = new FakeAccountDevToolsService();
            var controller = new AccountDevToolsController(service);

            await controller.SignOutUgsAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual(1, service.SignOutUgsCalls);
            StringAssert.Contains("данные входа сохранены", state.LastResult);
            StringAssert.Contains("cached credentials preserved", state.Diagnostics);
        }

        [Test]
        public void Controller_GivenPlayerAccountSession_WhenSignOutIsCalled_ThenForwardsAndPublishesSuccess()
        {
            var service = new FakeAccountDevToolsService();
            var controller = new AccountDevToolsController(service);
            int presentationChanges = 0;
            controller.PresentationChanged += () => presentationChanges++;

            controller.SignOutPlayerAccount();
            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual(1, service.SignOutPlayerAccountCalls);
            Assert.AreEqual("Сессия Unity Player Account завершена.", state.LastResult);
            StringAssert.Contains("Local UPA OAuth session cleared", state.Diagnostics);
            Assert.AreEqual(1, presentationChanges);
        }

        [Test]
        public void Controller_GivenPlayerAccountSignOutFailure_WhenCalled_ThenMovesExceptionToDiagnostics()
        {
            var service = new FakeAccountDevToolsService
            {
                SignOutPlayerAccountHandler = () => throw new InvalidOperationException("raw sign-out failure")
            };
            var controller = new AccountDevToolsController(service);

            controller.SignOutPlayerAccount();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("Выйти из Unity Player Account не удалось", state.LastResult);
            StringAssert.DoesNotContain("raw sign-out failure", state.LastResult);
            StringAssert.Contains("raw sign-out failure", state.Diagnostics);
        }

        [Test]
        public async Task Controller_GivenCachedIdentity_WhenClearIsCalled_ThenForwardsAndShowsShortSuccess()
        {
            var service = new FakeAccountDevToolsService();
            var controller = new AccountDevToolsController(service);

            await controller.ClearCachedIdentityAsync();
            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual(1, service.ClearIdentityCalls);
            Assert.AreEqual("Данные входа на устройстве очищены.", state.LastResult);
            StringAssert.Contains("PlayerData was not changed", state.Diagnostics);
        }

        [TestCase("Ensure")]
        [TestCase("Refresh")]
        [TestCase("Link")]
        [TestCase("Unlink")]
        [TestCase("SignOutUgs")]
        [TestCase("ClearIdentity")]
        public async Task Controller_GivenAsyncServiceException_WhenActionRuns_ThenMovesExceptionToDiagnostics(
            string action)
        {
            var service = new FakeAccountDevToolsService();
            ConfigureException(service, action, "raw async failure");
            var controller = new AccountDevToolsController(service);

            await InvokeAsync(controller, action);
            AccountDevToolsViewState state = controller.GetViewState();

            Assert.AreEqual("Операция завершилась ошибкой. Подробности — в диагностике.", state.LastResult);
            StringAssert.DoesNotContain("raw async failure", state.LastResult);
            StringAssert.Contains("raw async failure", state.Diagnostics);
            Assert.IsFalse(state.IsBusy);
        }

        [Test]
        public void Controller_GivenMissingConfiguration_WhenReported_ThenPublishesHelpDirectionAndDiagnostics()
        {
            var service = new FakeAccountDevToolsService { IsLocallyReady = false };
            var controller = new AccountDevToolsController(service);
            int presentationChanges = 0;
            controller.PresentationChanged += () => presentationChanges++;

            controller.ReportMissingConfiguration();
            AccountDevToolsViewState state = controller.GetViewState();

            StringAssert.Contains("выполни подготовку из справки", state.LastResult);
            StringAssert.DoesNotContain("cloudProjectId", state.LastResult);
            StringAssert.Contains("cloudProjectId", state.Diagnostics);
            Assert.IsFalse(controller.IsLocallyReady);
            Assert.AreEqual(1, presentationChanges);
        }

        [Test]
        public void Controller_GivenDashboardRequest_WhenOpened_ThenForwardsExactlyOnce()
        {
            var service = new FakeAccountDevToolsService();
            var controller = new AccountDevToolsController(service);

            controller.OpenDashboard();

            Assert.AreEqual(1, service.OpenDashboardCalls);
        }

        private static void ConfigureException(
            FakeAccountDevToolsService service,
            string action,
            string message)
        {
            Func<Exception> create = () => new InvalidOperationException(message);
            switch (action)
            {
                case "Ensure":
                    service.EnsureHandler = () => Task.FromException<AccountSnapshot>(create());
                    break;
                case "Refresh":
                    service.RefreshHandler = () => Task.FromException<AccountSnapshot>(create());
                    break;
                case "Link":
                    service.LinkHandler = () => Task.FromException<AccountLinkResult>(create());
                    break;
                case "Unlink":
                    service.UnlinkHandler = () => Task.FromException<AccountSnapshot>(create());
                    break;
                case "SignOutUgs":
                    service.SignOutUgsHandler = () => Task.FromException<AccountSnapshot>(create());
                    break;
                case "ClearIdentity":
                    service.ClearIdentityHandler = () => Task.FromException<AccountSnapshot>(create());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static Task InvokeAsync(AccountDevToolsController controller, string action)
        {
            switch (action)
            {
                case "Ensure":
                    return controller.EnsureSessionAsync();
                case "Refresh":
                    return controller.RefreshAsync();
                case "Link":
                    return controller.LinkAsync();
                case "Unlink":
                    return controller.UnlinkAsync();
                case "SignOutUgs":
                    return controller.SignOutUgsAsync();
                case "ClearIdentity":
                    return controller.ClearCachedIdentityAsync();
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private sealed class FakeAccountDevToolsService : IAccountDevToolsService
        {
            public AccountSnapshot SnapshotValue { get; set; } = AccountSnapshot.Unknown;
            public bool IsLocallyReady { get; set; } = true;
            public string HumanStatus { get; set; } = "Гость";
            public string Readiness { get; set; } = "readiness";
            public string Session { get; set; } = "session";
            public AccountSnapshot EnsureResult { get; set; } = AccountSnapshot.Unknown;
            public AccountSnapshot RefreshResult { get; set; } = AccountSnapshot.Unknown;
            public AccountLinkResult LinkResult { get; set; } = AccountLinkResult.Unknown();
            public AccountSnapshot UnlinkResult { get; set; } = AccountSnapshot.Unknown;
            public AccountSnapshot SignOutUgsResult { get; set; } = AccountSnapshot.Unknown;
            public AccountSnapshot ClearIdentityResult { get; set; } = AccountSnapshot.Unknown;
            public Func<Task<AccountSnapshot>> EnsureHandler { get; set; }
            public Func<Task<AccountSnapshot>> RefreshHandler { get; set; }
            public Func<Task<AccountLinkResult>> LinkHandler { get; set; }
            public Func<Task<AccountSnapshot>> UnlinkHandler { get; set; }
            public Func<Task<AccountSnapshot>> SignOutUgsHandler { get; set; }
            public Func<Task<AccountSnapshot>> ClearIdentityHandler { get; set; }
            public Action SignOutPlayerAccountHandler { get; set; }
            public int EnsureCalls { get; private set; }
            public int RefreshCalls { get; private set; }
            public int SignOutUgsCalls { get; private set; }
            public int SignOutPlayerAccountCalls { get; private set; }
            public int ClearIdentityCalls { get; private set; }
            public int OpenDashboardCalls { get; private set; }

            public AccountSnapshot Snapshot => SnapshotValue;
            public bool IsLocallyReadyForPlayerAccounts => IsLocallyReady;
            public string GetHumanStatusText() => HumanStatus;
            public string GetReadinessText() => Readiness;
            public string GetSessionText() => Session;

            public Task<AccountSnapshot> EnsureSessionAsync()
            {
                EnsureCalls++;
                return EnsureHandler?.Invoke() ?? Task.FromResult(EnsureResult);
            }

            public Task<AccountSnapshot> RefreshAsync()
            {
                RefreshCalls++;
                return RefreshHandler?.Invoke() ?? Task.FromResult(RefreshResult);
            }

            public Task<AccountLinkResult> LinkAsync()
            {
                return LinkHandler?.Invoke() ?? Task.FromResult(LinkResult);
            }

            public Task<AccountSnapshot> UnlinkAsync()
            {
                return UnlinkHandler?.Invoke() ?? Task.FromResult(UnlinkResult);
            }

            public Task<AccountSnapshot> SignOutUgsKeepingCredentialsAsync()
            {
                SignOutUgsCalls++;
                return SignOutUgsHandler?.Invoke() ?? Task.FromResult(SignOutUgsResult);
            }

            public void SignOutPlayerAccount()
            {
                SignOutPlayerAccountCalls++;
                SignOutPlayerAccountHandler?.Invoke();
            }

            public Task<AccountSnapshot> ClearCachedIdentityAsync()
            {
                ClearIdentityCalls++;
                return ClearIdentityHandler?.Invoke() ?? Task.FromResult(ClearIdentityResult);
            }

            public void OpenDashboard()
            {
                OpenDashboardCalls++;
            }
        }
    }
}
