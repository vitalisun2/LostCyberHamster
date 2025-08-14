using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class SigninModalController : ModalController
    {
        private Label _modalTitle => _modalContent.Q<Label>("modal__title");
        private Button _signinButton => _modalContent.Q<Button>("btn__signin");

        protected override ScreenEnum _modalAssetName => ScreenEnum.SigninModal;

        public SigninModalController(UIDocument uiDocument): base(uiDocument)
        {
        }

        protected override async Task OnShowAsync()
        {
        }

        protected override void OnSubscribeToEvents()
        {
            _signinButton?.RegisterCallback<ClickEvent>(OnClickLogin);
            AuthenticationManager.LinkingCompletedSuccess += OnLinkingCompletedSuccess;
            AuthenticationManager.LinkingCompletedFailed += OnLinkingCompletedFailed;
        }

        private void OnLinkingCompletedFailed()
        {
            Debug.Log("Linking failed");
            Close();
        }


        private void OnLinkingCompletedSuccess()
        {
            Debug.Log("Linking success");
            Close();
        }


        private async void OnClickLogin(ClickEvent evt)
        {
            await AuthenticationManager.LinkAnonymousAccountToUnityAsync();
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _signinButton?.UnregisterCallback<ClickEvent>(OnClickLogin);
        }
    }
}