using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class LoseModalController : ModalController
    {
        private Button _watchAdsButton => _modalContent.Q<Button>("btn__watch-ads");

        private VisualElement _restartButton => _modalContent.Q<VisualElement>("btn__repeat");

        private VisualElement _exitButton => _modalContent.Q<VisualElement>("btn__home");

        private Action _actionWatchAdd;
        private Action _actionRestart;
        private Action _actionExit;

        private GameResultModalPresentation _presentation;

        protected override ScreenEnum _modalAssetName => ScreenEnum.LoseModal;

        public LoseModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override Task OnShowAsync()
        {
            _presentation?.Restore();
            _presentation = GameResultModalPresentation.Apply(_root);
            _buttonCloseModal.style.display = DisplayStyle.None;
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _watchAdsButton?.RegisterCallback<ClickEvent>(OnClickWatchAds);
            _restartButton?.RegisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.RegisterCallback<ClickEvent>(OnClickExit);

        }

        private void OnClickExit(ClickEvent evt)
        {
            _actionExit?.Invoke();
        }


        private void OnClickRestart(ClickEvent evt)
        {
            _actionRestart?.Invoke();
        }


        private void OnClickWatchAds(ClickEvent evt)
        {
            _actionWatchAdd?.Invoke();
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _watchAdsButton?.UnregisterCallback<ClickEvent>(OnClickWatchAds);
            _restartButton?.UnregisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.UnregisterCallback<ClickEvent>(OnClickExit);
            _presentation?.Restore();
            _presentation = null;
        }

        public void SetRestartAction(Action value)
        {
            _actionRestart = value;
        }

        public void SetExitAction(Action value)
        {
            _actionExit = value;
        }

        public void SetWatchAdsAction(Action value)
        {
            _actionWatchAdd = value;
        }
    }
}
