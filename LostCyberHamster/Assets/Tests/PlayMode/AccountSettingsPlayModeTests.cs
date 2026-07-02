#if UNITY_INCLUDE_TESTS
using System;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using LostCyberHamster.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Tests.PlayMode
{
    public class AccountSettingsPlayModeTests
    {
        private GameObject _uiObject;
        private PanelSettings _panelSettings;
        private bool _ownsPanelSettings;

        [SetUp]
        public void SetUp()
        {
            AccountServiceProvider.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AccountServiceProvider.ResetForTests();
            if (_uiObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_uiObject);
            }

            if (_ownsPanelSettings && _panelSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(_panelSettings);
            }

            _uiObject = null;
            _panelSettings = null;
            _ownsPanelSettings = false;
        }

        [Test]
        public async Task SettingsModal_WhenGuest_ShowsSaveProgressEntry()
        {
            var account = FakeAccountService.Guest();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SettingsModalController(root);

            await controller.ShowAsync();

            var status = root.Q<Label>("settings__lbl-account-status");
            var button = root.Q<Button>("settings__btn-save-progress");

            Assert.IsNotNull(status);
            Assert.IsNotNull(button);
            AssertText(status, "account_guest");
            AssertText(button, "account_save_button");
            Assert.IsTrue(button.enabledSelf);
            Assert.AreEqual(1, account.IsLinkedCalls);
        }

        [Test]
        public async Task SettingsModal_WhenSaveProgressSucceeds_DisablesButtonAndShowsSavedState()
        {
            var account = FakeAccountService.Guest();
            account.LinkCompletion = new TaskCompletionSource<AccountLinkResult>();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SettingsModalController(root);

            await controller.ShowAsync();
            var status = root.Q<Label>("settings__lbl-account-status");
            var button = root.Q<Button>("settings__btn-save-progress");

            SendClick(button);
            await Task.Yield();

            Assert.IsFalse(button.enabledSelf);
            AssertText(status, "account_save_in_progress");
            Assert.AreEqual(1, account.LinkCalls);

            account.CompleteLink(AccountLinkResult.Success("player"));
            await WaitUntilAsync(() => account.RefreshCalls > 0 && account.Snapshot.IsLinked);

            AssertText(status, "account_saved");
            AssertText(button, "account_saved_button");
            Assert.IsFalse(button.enabledSelf);
        }

        [Test]
        public async Task SettingsModal_WhenSaveProgressFails_ReEnablesButtonAndShowsError()
        {
            var account = FakeAccountService.Guest();
            account.LinkCompletion = new TaskCompletionSource<AccountLinkResult>();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SettingsModalController(root);

            await controller.ShowAsync();
            var status = root.Q<Label>("settings__lbl-account-status");
            var button = root.Q<Button>("settings__btn-save-progress");

            SendClick(button);
            await Task.Yield();

            account.CompleteLink(AccountLinkResult.Failed("network"));
            await WaitUntilAsync(() => button.enabledSelf);

            AssertText(status, "account_save_error");
            AssertText(button, "account_save_button");
            Assert.IsFalse(account.Snapshot.IsLinked);
        }

        [Test]
        public async Task SettingsModal_WhenAccountAlreadyLinked_ShowsConflictAndKeepsGuest()
        {
            var account = FakeAccountService.Guest();
            account.LinkCompletion = new TaskCompletionSource<AccountLinkResult>();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SettingsModalController(root);

            await controller.ShowAsync();
            var status = root.Q<Label>("settings__lbl-account-status");
            var button = root.Q<Button>("settings__btn-save-progress");

            SendClick(button);
            await Task.Yield();

            account.CompleteLink(AccountLinkResult.AlreadyLinked("conflict"));
            await WaitUntilAsync(() => button.enabledSelf);

            AssertText(status, "account_already_linked");
            Assert.IsFalse(account.Snapshot.IsLinked);
        }

        [Test]
        public async Task SettingsModal_WhenLinked_ShowsSavedState()
        {
            var account = FakeAccountService.Linked();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SettingsModalController(root);

            await controller.ShowAsync();

            var status = root.Q<Label>("settings__lbl-account-status");
            var button = root.Q<Button>("settings__btn-save-progress");

            AssertText(status, "account_saved");
            AssertText(button, "account_saved_button");
            Assert.IsFalse(button.enabledSelf);
        }

        [Test]
        public async Task SigninModal_WhenLaterClicked_ClosesWithoutLinking()
        {
            var account = FakeAccountService.Guest();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SigninModalController(root);

            await controller.ShowAsync();
            var modal = root.Q<VisualElement>("modal");
            var laterButton = root.Q<Button>("btn__signin-later");

            SendClick(laterButton);
            await Task.Yield();

            Assert.AreEqual(DisplayStyle.None, modal.style.display.value);
            Assert.AreEqual(0, account.LinkCalls);
        }

        [Test]
        public async Task SigninModal_WhenSaveSucceeds_ShowsSavedAndCloses()
        {
            var account = FakeAccountService.Guest();
            account.LinkCompletion = new TaskCompletionSource<AccountLinkResult>();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SigninModalController(root);

            await controller.ShowAsync();
            var modal = root.Q<VisualElement>("modal");
            var status = root.Q<Label>("save-progress__status");
            var saveButton = root.Q<Button>("btn__signin");

            SendClick(saveButton);
            await Task.Yield();

            AssertText(status, "account_save_in_progress");

            account.CompleteLink(AccountLinkResult.Success("player"));
            await WaitUntilAsync(() => status.text == "account_saved");
            await Task.Delay(800);

            Assert.AreEqual(DisplayStyle.None, modal.style.display.value);
        }

        [Test]
        public async Task SigninModal_WhenSaveFails_StaysOpenAndReEnablesButtons()
        {
            var account = FakeAccountService.Guest();
            account.LinkCompletion = new TaskCompletionSource<AccountLinkResult>();
            AccountServiceProvider.SetForTests(account);
            var root = CreateModalRoot();
            var controller = new SigninModalController(root);

            await controller.ShowAsync();
            var modal = root.Q<VisualElement>("modal");
            var status = root.Q<Label>("save-progress__status");
            var saveButton = root.Q<Button>("btn__signin");
            var laterButton = root.Q<Button>("btn__signin-later");

            SendClick(saveButton);
            await Task.Yield();

            account.CompleteLink(AccountLinkResult.Failed("failed"));
            await WaitUntilAsync(() => saveButton.enabledSelf && laterButton.enabledSelf);

            Assert.AreEqual(DisplayStyle.Flex, modal.style.display.value);
            AssertText(status, "account_save_failed_retry");
        }

        private VisualElement CreateModalRoot()
        {
            _uiObject = new GameObject("AccountSettingsPlayModeTests_UIDocument");
            _panelSettings = CreatePanelSettings();
            var uiDocument = _uiObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = _panelSettings;

            var root = uiDocument.rootVisualElement;
            var modal = new VisualElement { name = "modal" };
            var closeButton = new Button { name = "btn_close-modal" };
            var content = new VisualElement { name = "modal__content" };
            modal.Add(closeButton);
            modal.Add(content);
            root.Add(modal);
            return root;
        }

        private PanelSettings CreatePanelSettings()
        {
#if UNITY_EDITOR
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset");
            if (panelSettings != null)
            {
                _ownsPanelSettings = false;
                return panelSettings;
            }
#endif

            _ownsPanelSettings = true;
            return ScriptableObject.CreateInstance<PanelSettings>();
        }

        private static async Task WaitUntilAsync(Func<bool> predicate, int maxFrames = 120)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Yield();
            }

            Assert.Fail("Condition was not met before timeout.");
        }

        private static void SendClick(Button button)
        {
            Assert.IsNotNull(button);
            using var evt = ClickEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        private static void AssertText(TextElement element, params string[] expected)
        {
            Assert.Contains(element.text, expected);
        }

        private sealed class FakeAccountService : IAccountService
        {
            public event Action<AccountSnapshot> StateChanged;

            public AccountSnapshot Snapshot { get; private set; }
            public TaskCompletionSource<AccountLinkResult> LinkCompletion { get; set; }
            public int IsLinkedCalls { get; private set; }
            public int RefreshCalls { get; private set; }
            public int LinkCalls { get; private set; }

            private FakeAccountService(AccountSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public static FakeAccountService Guest()
            {
                return new FakeAccountService(new AccountSnapshot(AccountState.Guest, "guest-player", true, false, string.Empty));
            }

            public static FakeAccountService Linked()
            {
                return new FakeAccountService(new AccountSnapshot(AccountState.Linked, "linked-player", true, true, string.Empty));
            }

            public Task<AccountSnapshot> EnsureSignedInAsync()
            {
                return Task.FromResult(Snapshot);
            }

            public Task<AccountSnapshot> RefreshLinkStateAsync()
            {
                RefreshCalls++;
                return Task.FromResult(Snapshot);
            }

            public Task<bool> IsLinkedAsync()
            {
                IsLinkedCalls++;
                return Task.FromResult(Snapshot.IsLinked);
            }

            public async Task<AccountLinkResult> LinkUnityAccountAsync()
            {
                LinkCalls++;
                var result = LinkCompletion == null
                    ? AccountLinkResult.Success(Snapshot.PlayerId)
                    : await LinkCompletion.Task;

                if (result.IsSuccess)
                {
                    Snapshot = new AccountSnapshot(AccountState.Linked, Snapshot.PlayerId, true, true, string.Empty);
                    StateChanged?.Invoke(Snapshot);
                }

                return result;
            }

            public Task<AccountLinkResult> LinkUnityAccountWithAccessTokenAsync(string accessToken)
            {
                return LinkUnityAccountAsync();
            }

            public Task<AccountSnapshot> UnlinkUnityAccountAsync()
            {
                Snapshot = new AccountSnapshot(AccountState.Guest, Snapshot.PlayerId, true, false, string.Empty);
                StateChanged?.Invoke(Snapshot);
                return Task.FromResult(Snapshot);
            }

            public void CompleteLink(AccountLinkResult result)
            {
                LinkCompletion.SetResult(result);
            }
        }
    }
}
#endif
