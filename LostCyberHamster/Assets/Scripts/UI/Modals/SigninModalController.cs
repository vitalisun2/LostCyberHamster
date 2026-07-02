using System.Threading.Tasks;
using LostCyberHamster.Account;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class SigninModalController : ModalController
    {
        private Button _signinButton => _modalContent.Q<Button>("btn__signin");
        private Button _laterButton => _modalContent.Q<Button>("btn__signin-later");
        private Label _statusLabel => _modalContent.Q<Label>("save-progress__status");
        private bool _isLinking;

        protected override ScreenEnum _modalAssetName => ScreenEnum.SigninModal;

        public SigninModalController(UIDocument uiDocument): base(uiDocument)
        {
        }

        internal SigninModalController(VisualElement root): base(root)
        {
        }

        protected override Task OnShowAsync()
        {
            _isLinking = false;
            SetStatus(string.Empty);
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _signinButton?.RegisterCallback<ClickEvent>(OnClickLogin);
            _laterButton?.RegisterCallback<ClickEvent>(OnClickLater);
        }

        private void OnClickLater(ClickEvent evt)
        {
            Close();
        }

        private async void OnClickLogin(ClickEvent evt)
        {
            if (_isLinking)
            {
                return;
            }

            _isLinking = true;
            SetButtonsEnabled(false);
            SetStatus(Text("account_save_in_progress"));

            try
            {
                var result = await AccountServiceProvider.Current.LinkUnityAccountAsync();
                if (result.IsSuccess)
                {
                    SetStatus(Text("account_saved"));
                    await Task.Delay(700);
                    Close();
                    return;
                }

                SetStatus(GetFailureText(result));
            }
            finally
            {
                _isLinking = false;
                SetButtonsEnabled(true);
            }
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _signinButton?.UnregisterCallback<ClickEvent>(OnClickLogin);
            _laterButton?.UnregisterCallback<ClickEvent>(OnClickLater);
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _signinButton?.SetEnabled(enabled);
            _laterButton?.SetEnabled(enabled);
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text ?? string.Empty;
            }
        }

        private static string GetFailureText(AccountLinkResult result)
        {
            return result.Status == AccountLinkStatus.AlreadyLinked
                ? Text("account_already_linked")
                : Text("account_save_failed_retry");
        }

        private static string Text(string key)
        {
            return LocalizationManager.GetLocalizedString(key) ?? key;
        }
    }
}
