using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает новый уровень и начисленные Development Points.
    /// </summary>
    public sealed class LevelUpModalController : ModalController
    {
        private readonly Action _closeAction;

        private int _previousLevel;
        private int _currentLevel;
        private int _pointsAwarded;
        private Action _okAction;

        private Label Title =>
            _modalContent.Q<Label>("level-up-title");
        private Label Transition =>
            _modalContent.Q<Label>("level-up-transition");
        private Label Reward =>
            _modalContent.Q<Label>("level-up-development-reward");
        private Button OkButton =>
            _modalContent.Q<Button>("btn_level_up_ok");

        protected override ScreenEnum _modalAssetName =>
            ScreenEnum.LevelUpModal;

        public LevelUpModalController(
            UIDocument uiDocument,
            Action closeAction)
            : base(uiDocument)
        {
            _closeAction = closeAction ??
                throw new ArgumentNullException(nameof(closeAction));
        }

        /// <summary>
        /// Задаёт level transition и количество points из этого перехода.
        /// </summary>
        public void SetLevelUpData(
            int previousLevel,
            int currentLevel,
            int pointsAwarded)
        {
            _previousLevel = previousLevel;
            _currentLevel = currentLevel;
            _pointsAwarded = pointsAwarded;
        }

        public void SetOkAction(Action action)
        {
            _okAction = action;
        }

        protected override Task OnShowAsync()
        {
            _buttonCloseModal.style.display = DisplayStyle.None;
            Title.text = Localize("level_up_title");
            Transition.text = FormatLocalized(
                "level_up_transition",
                _previousLevel.ToString(),
                _currentLevel.ToString());
            Reward.text = FormatLocalized(
                "level_up_development_reward",
                _pointsAwarded.ToString());
            OkButton.text = Localize("level_up_ok");
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            OkButton?.RegisterCallback<ClickEvent>(OnOkClicked);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            OkButton?.UnregisterCallback<ClickEvent>(OnOkClicked);
        }

        private void OnOkClicked(ClickEvent clickEvent)
        {
            Action queuedAction = _okAction;
            _okAction = null;
            _closeAction.Invoke();
            queuedAction?.Invoke();
        }

        private static string Localize(string key)
        {
            string localized =
                LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(localized)
                ? key ?? string.Empty
                : localized;
        }

        private static string FormatLocalized(
            string key,
            params string[] values)
        {
            return string.Format(Localize(key), values);
        }
    }
}
