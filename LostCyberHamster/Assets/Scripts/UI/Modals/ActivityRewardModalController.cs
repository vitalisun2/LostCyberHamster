using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает один предъявленный приз; receipt ACK не повторяет Claim.</summary>
    public sealed class ActivityRewardModalController : ModalController
    {
        private readonly Action _close;
        private ActivityRewardSnapshot _expected;
        private bool _received;
        private bool _busy;
        private GameResultModalPresentation _presentation;
        protected override ScreenEnum _modalAssetName => ScreenEnum.ActivityRewardModal;
        private Button Claim => _modalContent.Q<Button>("return-reward-claim");
        private Button Later => _modalContent.Q<Button>("return-reward-later");
        private Label Status => _modalContent.Q<Label>("return-reward-status");

        public ActivityRewardModalController(UIDocument document, Action close) : base(document) { _close = close; }
        public void SetReward(ActivityRewardSnapshot expected) => _expected = expected;

        protected override Task OnShowAsync()
        {
            _buttonCloseModal.style.display = DisplayStyle.None;
            _busy = false; _received = _expected?.Claimed == true;
            _modalContent.Q<Label>("return-reward-title").text = _expected == null ? ActivityUiText.Get("unavailable") :
                ActivityUiText.RewardTitle(_expected);
            _modalContent.Q<Label>("return-reward-amount").text = _expected == null ? string.Empty :
                ActivityUiText.Amount(_expected.Coins, _expected.Gems);
            _modalContent.Q<Label>("return-reward-origin").text = _expected?.OriginDay ?? string.Empty;
            _modalContent.Q<Label>("return-reward-marks").text = _expected?.Step == 7 ? "✓  ✓  ✓  ✓  ✓  ✓  ✓" : "✓";
            Refresh();
            return Task.CompletedTask;
        }

        private void Refresh()
        {
            Claim.text = _received ? ActivityUiText.Get("done") : ActivityUiText.Get("claim");
            Later.text = _received ? ActivityUiText.Get("close") : ActivityUiText.Get("later");
            Claim.SetEnabled(!_busy && _expected?.IsCurrent == true && ReturnActivityService.CanMutate);
            Status.text = _received ? ActivityUiText.Get("received") : ActivityUiText.Get("claim_hint");
        }

        protected override void OnSubscribeToEvents()
        {
            Claim.clicked += OnClaim; Later.clicked += OnLater;
            _presentation ??= GameResultModalPresentation.Apply(_root,
                _modalContent.Q("reward-modal-viewport"), _modalContent.Q("reward-modal-frame"),
                _modalContent.Q("reward-modal-design"), new Vector2(1672, 941), ModalScaleMode.Contain, useSafeArea: true);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            Claim.clicked -= OnClaim; Later.clicked -= OnLater;
            _presentation?.Restore(); _presentation = null;
        }

        private void OnClaim()
        {
            if (_busy) return;
            if (_received) { CloseReceipt(); return; }
            _busy = true; Claim.SetEnabled(false);
            var result = ReturnActivityRewardService.Claim(_expected);
            _busy = false;
            _received = result == ActivityClaimResult.Claimed || result == ActivityClaimResult.AlreadyClaimed;
            Refresh();
            if (!_received) Status.text = ActivityUiText.Get(result == ActivityClaimResult.SaveFailed ? "save_failed" : "unavailable");
        }

        private void OnLater()
        {
            if (_busy) return;
            if (_received) CloseReceipt(); else _close();
        }

        private void CloseReceipt()
        {
            if (_busy) return;
            _busy = true;
            if (_expected?.IsCurrent != true) { _close(); return; }
            if (ReturnActivityRewardService.Acknowledge(_expected)) _close();
            else
            {
                _busy = false;
                Status.text = ActivityUiText.Get("save_failed");
            }
        }
    }
}
