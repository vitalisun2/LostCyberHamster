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
        private Action _buyRevive;
        private Func<bool> _canContinue;
        private Func<bool> _canBuyRevive;
        private Button CrystalButton => _modalContent.Q<Button>("btn__revive-crystal");
        private IVisualElementScheduledItem _reviveSchedule;
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
            if (_canContinue?.Invoke() == true)
                MonetizationEvent.Record("offer", "revive", QuestManager.CurrentAttemptPreview.AttemptId);
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _presentation ??= GameResultModalPresentation.Apply(_root);
            _watchAdsButton?.RegisterCallback<ClickEvent>(OnClickWatchAds);
            CrystalButton?.RegisterCallback<ClickEvent>(OnClickCrystal);
            _reviveSchedule?.Pause();
            _reviveSchedule = _modalContent.schedule.Execute(UpdateAdvertisementState).Every(200);
            _restartButton?.RegisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.RegisterCallback<ClickEvent>(OnClickExit);
            _ads = RewardedAdService.Instance;
            _ads.Changed += UpdateAdvertisementState;
            UpdateAdvertisementState();
        }

        private void OnClickCrystal(ClickEvent evt) => _buyRevive?.Invoke();

        private bool NavigationBlocked => _ownedAdRequest != null &&
            (_ownedAdRequest.IsNativePending || _ownedAdRequest.State == RewardedAdState.Settling);

        private void OnClickExit(ClickEvent evt)
        {
            if (!NavigationBlocked) _actionExit?.Invoke();
        }


        private void OnClickRestart(ClickEvent evt)
        {
            if (!NavigationBlocked) _actionRestart?.Invoke();
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
            _reviveSchedule?.Pause();
            _reviveSchedule = null;
            CrystalButton?.UnregisterCallback<ClickEvent>(OnClickCrystal);
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
            bool canContinue = _canContinue?.Invoke() ?? true;
            _modalContent.Q("revive-options")?.SetEnabled(canContinue);
            var options = _modalContent.Q("revive-options");
            if (options != null) options.style.display = canContinue ? DisplayStyle.Flex : DisplayStyle.None;
            _watchAdsButton?.SetEnabled(canContinue && _ads.CanRequest);
            CrystalButton?.SetEnabled(_canBuyRevive?.Invoke() == true);
            bool blockNavigation = NavigationBlocked;
            _restartButton?.SetEnabled(!blockNavigation);
            _exitButton?.SetEnabled(!blockNavigation);

            // Используем существующую текстовую плашку, сохраняя локализацию и геометрию.
            var message = _modalContent.Q<Label>("revive-status");
            if (message == null)
                return;
            string key = _ownedAdRequest != null && !_ownedAdRequest.IsFinished ? _ads.StatusKey : string.Empty;
            if (!canContinue) key = "revive_used";
            else if (string.IsNullOrEmpty(key) || key == "ads_reward_granted") key = "revive_once";
            message.text = LocalizationManager.GetLocalizedString(key);
        }

        public void SetReviveActions(Func<bool> canContinue, Func<bool> canBuy, Action buy)
        {
            _canContinue = canContinue;
            _canBuyRevive = canBuy;
            _buyRevive = buy;
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
