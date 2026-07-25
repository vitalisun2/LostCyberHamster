using System.Threading.Tasks;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public sealed class AccountPromptModalController : ModalController
    {
        private const long _laterMessageDurationMs = 3000;

        protected override ScreenEnum _modalAssetName => ScreenEnum.AccountPromptModal;

        private Button _buttonLinkAccount =>
            _modalContent.Q<Button>("account-prompt-modal__btn-link-account");

        private Button _buttonLater =>
            _modalContent.Q<Button>("account-prompt-modal__btn-later");

        private Label _transientMessage;
        private IVisualElementScheduledItem _messageRemoval;

        public AccountPromptModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override Task OnShowAsync()
        {
            RemoveTransientMessage();
            _buttonCloseModal.style.display = DisplayStyle.None;

            var laterMessage = _modalContent.Q<Label>("account-prompt-modal__later-message");
            if (laterMessage != null)
            {
                laterMessage.text = LocalizationManager.GetLocalizedString("account_prompt_later_message");
                laterMessage.style.display = DisplayStyle.None;
            }

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
            RemoveTransientMessage();
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
                Close();
                return;
            }

            RemoveTransientMessage();
            laterMessage.RemoveFromHierarchy();
            laterMessage.style.display = DisplayStyle.Flex;
            laterMessage.pickingMode = PickingMode.Ignore;
            _root.Add(laterMessage);
            _transientMessage = laterMessage;

            Close();
            _messageRemoval = laterMessage.schedule
                .Execute(RemoveTransientMessage)
                .StartingIn(_laterMessageDurationMs);
        }

        private void RemoveTransientMessage()
        {
            _messageRemoval?.Pause();
            _messageRemoval = null;
            _transientMessage?.RemoveFromHierarchy();
            _transientMessage = null;
        }
    }
}
