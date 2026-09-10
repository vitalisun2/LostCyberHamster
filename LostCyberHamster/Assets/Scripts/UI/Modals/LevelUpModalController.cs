using System;
using System.Threading.Tasks;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает новый уровень и сохранённые выплаты DP и монет.
    /// </summary>
    public sealed class LevelUpModalController : ModalController
    {
        private readonly Action _closeAction;

        private int _previousLevel;
        private int _currentLevel;
        private int _pointsAwarded;
        private int _coinsAwarded;
        private Action _okAction;
        private Action _shieldAction;
        private Action _developmentAction;
        private Func<LevelUpAction, bool> _acceptanceAction;
        private GameResultModalPresentation _presentation;
        private bool _hasAccepted;

        private Label Title =>
            _modalContent.Q<Label>("level-up-title");
        private Label Transition =>
            _modalContent.Q<Label>("level-up-transition");
        private Label Reward =>
            _modalContent.Q<Label>("level-up-development-reward");
        private Button OkButton =>
            _modalContent.Q<Button>("btn_level_up_ok");
        private Button ShieldButton => _modalContent.Q<Button>("btn_level_up_shield");

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
        /// Задаёт диапазон уровней и фактически начисленные в нём награды.
        /// </summary>
        public void SetLevelUpData(
            int previousLevel,
            int currentLevel,
            int pointsAwarded, int coinsAwarded)
        {
            _previousLevel = previousLevel;
            _currentLevel = currentLevel;
            _pointsAwarded = pointsAwarded;
            _coinsAwarded = coinsAwarded;
        }

        public void SetOkAction(Action action)
        {
            _okAction = action;
        }

        public void SetAcceptanceAction(Func<LevelUpAction, bool> action) => _acceptanceAction = action;
        public void SetShieldAction(Action action) => _shieldAction = action;
        public void SetDevelopmentAction(Action action) => _developmentAction = action;

        protected override Task OnShowAsync()
        {
            // Новый показ принимает одно подтверждение.
            _buttonCloseModal.style.display = DisplayStyle.None;
            _hasAccepted = false;
            OkButton.SetEnabled(true);

            // Подставляем текущие данные в локализованный текст.
            Title.text = FormatLocalized("progression_level_title", _currentLevel.ToString());
            Transition.text = FormatLocalized(
                "level_up_transition",
                _previousLevel.ToString(),
                _currentLevel.ToString());
            Reward.text = _coinsAwarded > 0
                ? _pointsAwarded > 0
                    ? FormatLocalized("level_up_mixed_reward", _pointsAwarded.ToString(), _coinsAwarded.ToString())
                    : FormatLocalized("level_up_coins_reward", _coinsAwarded.ToString())
                : FormatLocalized("level_up_development_reward", _pointsAwarded.ToString());
            bool developmentComplete = CharacterDevelopmentService.IsDevelopmentComplete(GameDataManager.PlayerData);
            _modalContent.Q<Label>("level-up-free-points").text = developmentComplete
                ? Localize("level_up_development_complete")
                : FormatLocalized("progression_free_points", (GameDataManager.PlayerData?.DevelopmentPoints ?? 0).ToString());
            var options = _modalContent.Q<VisualElement>("level-up-options");
            options.Clear();
            foreach (string option in PlayerLevelRewardViewModel.GetOptions())
            {
                var label = new Label(option) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("level-up-modal__option");
                options.Add(label);
            }
            OkButton.text = Localize("first_session_continue");
            ShieldButton.style.display = _shieldAction != null || !developmentComplete ? DisplayStyle.Flex : DisplayStyle.None;
            ShieldButton.text = Localize(_shieldAction != null ? "first_session_open_shield" : "progression_open_development");
            ShieldButton.SetEnabled(true);
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            // Восстанавливаем композицию после повторного включения UI.
            _presentation ??= GameResultModalPresentation.Apply(
                _root,
                _modalContent.Q<VisualElement>("reward-modal-viewport"),
                _modalContent.Q<VisualElement>("reward-modal-frame"),
                _modalContent.Q<VisualElement>("reward-modal-design"),
                new Vector2(1672f, 941f),
                ModalScaleMode.Contain,
                useSafeArea: true);

            // Сохраняем состояние подтверждения без повторных callbacks.
            OkButton?.SetEnabled(!_hasAccepted);
            OkButton?.UnregisterCallback<ClickEvent>(OnOkClicked);
            OkButton?.RegisterCallback<ClickEvent>(OnOkClicked);
            ShieldButton?.UnregisterCallback<ClickEvent>(OnShieldClicked);
            ShieldButton?.RegisterCallback<ClickEvent>(OnShieldClicked);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            // Освобождаем действие текущего дерева.
            OkButton?.UnregisterCallback<ClickEvent>(OnOkClicked);
            ShieldButton?.UnregisterCallback<ClickEvent>(OnShieldClicked);

            // Возвращаем общий host следующему окну.
            _presentation?.Restore();
            _presentation = null;
        }

        private void OnOkClicked(ClickEvent clickEvent)
        {
            Accept(_okAction, LevelUpAction.Continue);
        }

        private void OnShieldClicked(ClickEvent clickEvent) => Accept(_shieldAction ?? _developmentAction,
            _shieldAction != null ? LevelUpAction.Shield : LevelUpAction.Development);

        private void Accept(Action action, LevelUpAction choice)
        {
            // Закрытие и отложенный маршрут выполняются один раз за показ.
            if (_hasAccepted)
                return;
            try
            {
                if (_acceptanceAction != null && !_acceptanceAction(choice))
                    return;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LevelUp] Acknowledgment failed ({exception.GetType().Name}).");
                OkButton.text = Localize("btn_retry");
                return;
            }
            _hasAccepted = true;
            OkButton?.SetEnabled(false);
            ShieldButton?.SetEnabled(false);

            // Освобождаем окно до запуска следующего маршрута.
            Action queuedAction = action;
            _okAction = null;
            _shieldAction = null;
            _developmentAction = null;
            _acceptanceAction = null;
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
