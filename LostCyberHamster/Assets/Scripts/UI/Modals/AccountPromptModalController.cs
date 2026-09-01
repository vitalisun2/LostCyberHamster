using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public sealed class AccountPromptModalController : ModalController
    {
        private const long LaterMessageDurationMs = 3000;
        private const string ModalStyleClass = "modal--account-prompt";

        protected override ScreenEnum _modalAssetName => ScreenEnum.AccountPromptModal;

        private Button _buttonLinkAccount =>
            _modalContent.Q<Button>("account-prompt-modal__btn-link-account");

        private Button _buttonLater =>
            _modalContent.Q<Button>("account-prompt-modal__btn-later");

        private Label _transientMessage;
        private IVisualElementScheduledItem _messageRemoval;
        private bool _preserveTransientMessageOnClose;

        public AccountPromptModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override Task OnShowAsync()
        {
            RemoveTransientMessage();
            _modal.EnableInClassList(ModalStyleClass, true);
            _buttonCloseModal.style.display = DisplayStyle.None;

            var laterMessage = _modalContent.Q<Label>("account-prompt-modal__later-message");
            if (laterMessage != null)
                laterMessage.style.display = DisplayStyle.None;

            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonLinkAccount?.UnregisterCallback<ClickEvent>(OnClickLinkAccount);
            _buttonLinkAccount?.RegisterCallback<ClickEvent>(OnClickLinkAccount);
            _buttonLater?.UnregisterCallback<ClickEvent>(OnClickLater);
            _buttonLater?.RegisterCallback<ClickEvent>(OnClickLater);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonLinkAccount?.UnregisterCallback<ClickEvent>(OnClickLinkAccount);
            _buttonLater?.UnregisterCallback<ClickEvent>(OnClickLater);
            _modal.EnableInClassList(ModalStyleClass, false);

            if (!_preserveTransientMessageOnClose)
                RemoveTransientMessage();

            _preserveTransientMessageOnClose = false;
        }

        private void OnClickLinkAccount(ClickEvent evt)
        {
            SettingsScreenController.OpenFrom(ScreenEnum.HomeScreen);
        }

        private void OnClickLater(ClickEvent evt)
        {
            var laterMessage = _modalContent.Q<Label>("account-prompt-modal__later-message");
            if (laterMessage == null)
            {
                Hide();
                UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
                return;
            }

            RemoveTransientMessage();
            laterMessage.RemoveFromHierarchy();
            laterMessage.style.display = DisplayStyle.Flex;
            laterMessage.pickingMode = PickingMode.Ignore;
            _root.Add(laterMessage);
            _transientMessage = laterMessage;

            // Прячем окно сразу, сохраняя дерево до штатного закрытия UIManager.
            _preserveTransientMessageOnClose = true;
            Hide();
            _messageRemoval = laterMessage.schedule
                .Execute(RemoveTransientMessage)
                .StartingIn(LaterMessageDurationMs);
            UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
        }

        private void RemoveTransientMessage()
        {
            _messageRemoval?.Pause();
            _messageRemoval = null;
            _transientMessage?.RemoveFromHierarchy();
            _transientMessage = null;
            _preserveTransientMessageOnClose = false;
        }
    }
}
