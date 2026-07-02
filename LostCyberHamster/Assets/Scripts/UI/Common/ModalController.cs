using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public abstract class ModalController : IScreenController
    {
        protected abstract ScreenEnum _modalAssetName { get; }
        protected VisualElement _root;
        protected Button _buttonCloseModal => _root.Q<Button>("btn_close-modal");
        protected VisualElement _modal => _root.Q<VisualElement>("modal");
        protected VisualElement _modalContent => _modal.Q<VisualElement>("modal__content");

        public ScreenEnum Type => _modalAssetName;

        protected ModalController(UIDocument uiDocument)
        {
            _root = uiDocument.rootVisualElement;
        }

        protected ModalController(VisualElement root)
        {
            _root = root;
        }

        public async Task ShowAsync()
        {
            var asset = await Addressables.LoadAssetAsync<VisualTreeAsset>(_modalAssetName.ToString()).Task;
            _modalContent.Clear();
            asset.CloneTree(_modalContent);
            _buttonCloseModal.style.display = DisplayStyle.Flex;
            await OnShowAsync();
            SubscribeToEvents();
            _modal.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _modal.style.display = DisplayStyle.None;
        }

        public void Close()
        {
            _modal.style.display = DisplayStyle.None;
            _modalContent.Clear();
        }

        private void OnClickBtnCloseModal(ClickEvent evt)
        {
            Close();
        }

        public void SubscribeToEvents()
        {
            _buttonCloseModal.RegisterCallback<ClickEvent>(OnClickBtnCloseModal);
            OnSubscribeToEvents();
        }

        public void UnsubscribeFromEvents()
        {
            _buttonCloseModal.UnregisterCallback<ClickEvent>(OnClickBtnCloseModal);
            OnUnsubscribeFromEvents();
        }

        protected abstract Task OnShowAsync();
        protected abstract void OnSubscribeToEvents();
        protected abstract void OnUnsubscribeFromEvents();
    }
}
