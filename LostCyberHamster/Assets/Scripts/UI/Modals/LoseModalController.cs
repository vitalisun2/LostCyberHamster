using System;
using System.Threading.Tasks;
using GameAds;
using UnityEngine.UIElements;
using Vues.GameCore;

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
        private RewardedAdService _ads;
        private RewardedAdRequest _ownedAdRequest;

        protected override ScreenEnum _modalAssetName => ScreenEnum.LoseModal;

        public LoseModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override Task OnShowAsync()
        {
            _presentation?.Restore();
            _presentation = GameResultModalPresentation.Apply(_root);
            _buttonCloseModal.style.display = DisplayStyle.None;
            UpdateAdvertisementState();
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _presentation ??= GameResultModalPresentation.Apply(_root);
            _watchAdsButton?.RegisterCallback<ClickEvent>(OnClickWatchAds);
            _restartButton?.RegisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.RegisterCallback<ClickEvent>(OnClickExit);
            _ads = RewardedAdService.Instance;
            _ads.Changed += UpdateAdvertisementState;
            UpdateAdvertisementState();
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
            if (_ads != null)
            {
                _ads.Changed -= UpdateAdvertisementState;
                _ads.CancelContext(_ownedAdRequest);
                _ownedAdRequest = null;
            }
            _watchAdsButton?.UnregisterCallback<ClickEvent>(OnClickWatchAds);
            _restartButton?.UnregisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.UnregisterCallback<ClickEvent>(OnClickExit);
            _presentation?.Restore();
            _presentation = null;
        }

        private void UpdateAdvertisementState()
        {
            if (_ads == null)
                return;
            _watchAdsButton?.SetEnabled(_ads.CanRequest);
            var request = _ownedAdRequest;
            bool blockNavigation = request != null &&
                (request.State == RewardedAdState.ShowSubmitted ||
                 request.State == RewardedAdState.Showing);
            _restartButton?.SetEnabled(!blockNavigation);
            _exitButton?.SetEnabled(!blockNavigation);

            // Используем существующую текстовую плашку, сохраняя локализацию и геометрию.
            var message = _modalContent.Q<LocalizedLabel>(className: "game-result-modal__message--watch-ad");
            if (message == null)
                return;
            string key = _ads.StatusKey;
            if (string.IsNullOrEmpty(key) || key == "ads_reward_granted")
                key = "fail_watch_ad_to_continue";
            message.key = key;
            message.text = LocalizationManager.GetLocalizedString(key);
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

        public void SetAdvertisementRequest(RewardedAdRequest request)
        {
            _ownedAdRequest = request;
            UpdateAdvertisementState();
        }
    }
}
