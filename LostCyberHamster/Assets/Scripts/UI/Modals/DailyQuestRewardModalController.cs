using System;
using System.Threading.Tasks;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает и выдаёт общую награду завершённого Daily-набора.
    /// </summary>
    public sealed class DailyQuestRewardModalController : ModalController
    {
        private readonly Action _closeAction;
        private GameResultModalPresentation _presentation;
        private bool _claimStarted;
        private DailyCommonRewardSnapshot _reward;
        private bool _showNext;
        private Button LaterButton => _modalContent.Q<Button>("btn_daily_quest_reward_later");

        private Label Title =>
            _modalContent.Q<Label>("daily-quest-reward-title");

        private Button ClaimButton =>
            _modalContent.Q<Button>("btn_daily_quest_reward_claim");

        private Label ClaimLabel =>
            _modalContent.Q<Label>("daily-quest-reward-claim-label");

        private Label RewardCaption =>
            _modalContent.Q<Label>("daily-quest-reward-caption");

        private Label RewardAmount =>
            _modalContent.Q<Label>("daily-quest-reward-amount");

        private VisualElement RewardImage =>
            _modalContent.Q<VisualElement>("daily-quest-reward-image");

        protected override ScreenEnum _modalAssetName =>
            ScreenEnum.DailyQuestRewardModal;

        public DailyQuestRewardModalController(
            UIDocument uiDocument,
            Action closeAction)
            : base(uiDocument)
        {
            _closeAction = closeAction ??
                throw new ArgumentNullException(nameof(closeAction));
        }

        /// <summary>
        /// Заполняет модальное окно актуальной общей наградой.
        /// </summary>
        protected override Task OnShowAsync()
        {
            // Новый показ принимает одну награду из текущего набора или очереди.
            _buttonCloseModal.style.display = DisplayStyle.None;
            _claimStarted = false;
            _reward = QuestManager.GetDailyCommonReward();
            _showNext = false;
            LaterButton.text = Localize("first_session_later");
            _modalContent.Q<Label>("daily-quest-reward-progress").text = "✓  ✓  ✓";
            _modalContent.Q<Label>("daily-quest-reward-origin").text = string.IsNullOrEmpty(_reward?.OriginDate)
                ? Localize("progression_saved_set") : _reward.OriginDate;

            // Тип и количество остаются данными QuestManager.
            Title.text = Localize("quests_daily_all_completed");
            ClaimLabel.text = Localize("btn_get");
            RewardCaption.text = Localize("quests_daily_reward_caption");
            RewardAmount.text =
                (_reward?.Amount ?? 0).ToString();
            RewardImage.style.backgroundImage =
                new StyleBackground(ResourceUIHelper.GetResourceImage(
                    _reward?.RewardType ?? QuestManager.DailyCommonRewardType));
            ClaimButton.SetEnabled(
                QuestManager.CanClaimDailyCommonRewardSnapshot(_reward));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Восстанавливает композицию и подключает получение общей награды.
        /// </summary>
        protected override void OnSubscribeToEvents()
        {
            LaterButton?.RegisterCallback<ClickEvent>(OnLaterClicked);
            // Восстанавливаем композицию после повторного включения UI.
            _presentation ??= GameResultModalPresentation.Apply(
                _root,
                _modalContent.Q<VisualElement>("reward-modal-viewport"),
                _modalContent.Q<VisualElement>("reward-modal-frame"),
                _modalContent.Q<VisualElement>("reward-modal-design"),
                new Vector2(1672f, 941f),
                ModalScaleMode.Contain,
                useSafeArea: true);

            // Сохраняем защиту выдачи и подключаем callback один раз.
            ClaimButton?.SetEnabled(
                _showNext || !_claimStarted && QuestManager.CanClaimDailyCommonRewardSnapshot(_reward));
            ClaimButton?.UnregisterCallback<ClickEvent>(OnClaimClicked);
            ClaimButton?.RegisterCallback<ClickEvent>(OnClaimClicked);
        }

        /// <summary>
        /// Отключает получение общей награды и возвращает общий modal host.
        /// </summary>
        protected override void OnUnsubscribeFromEvents()
        {
            LaterButton?.UnregisterCallback<ClickEvent>(OnLaterClicked);
            // Освобождаем действие текущего дерева.
            ClaimButton?.UnregisterCallback<ClickEvent>(OnClaimClicked);

            // Возвращаем общий host следующему окну.
            _presentation?.Restore();
            _presentation = null;
        }

        /// <summary>
        /// Выдаёт одну общую награду; после отказа обновляет доступность кнопки.
        /// </summary>
        private void OnClaimClicked(ClickEvent clickEvent)
        {
            if (_reward == null || _reward.ProfileId != GameDataManager.ProfileId || _reward.Generation != GameDataManager.Generation)
            {
                _closeAction.Invoke();
                return;
            }
            if (_showNext)
            {
                _ = OnShowAsync();
                return;
            }
            // Защищаем очередь до синхронной выдачи и её UI-событий.
            if (_claimStarted)
                return;
            _claimStarted = true;
            ClaimButton?.SetEnabled(false);

            // Успешный показ закрывается один раз, следующая награда ждёт нового показа.
            bool claimed;
            try { claimed = QuestManager.ClaimDailyCommonReward(_reward); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _claimStarted = false;
                ClaimButton?.SetEnabled(QuestManager.CanClaimDailyCommonRewardSnapshot(_reward));
                return;
            }
            if (claimed)
            {
                if (QuestManager.CanClaimDailyCommonReward)
                {
                    _showNext = true;
                    RewardCaption.text = Localize("progression_reward_received");
                    ClaimLabel.text = Localize("progression_next_reward");
                    ClaimButton.SetEnabled(true);
                    return;
                }
                _closeAction.Invoke();
                return;
            }

            // Повторная попытка зависит от актуальной доступности награды.
            _claimStarted = false;
            ClaimButton?.SetEnabled(QuestManager.CanClaimDailyCommonRewardSnapshot(_reward));
        }

        private void OnLaterClicked(ClickEvent evt) => _closeAction.Invoke();

        /// <summary>
        /// Возвращает локализованный текст с безопасным fallback.
        /// </summary>
        private static string Localize(string key)
        {
            string localized =
                LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(localized)
                ? key
                : localized;
        }
    }
}
