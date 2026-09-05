using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

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

            // Тип и количество остаются данными QuestManager.
            Title.text = Localize("quests_daily_all_completed");
            ClaimLabel.text = Localize("btn_get");
            RewardCaption.text = Localize("quests_daily_reward_caption");
            RewardAmount.text =
                QuestManager.DailyCommonRewardAmount.ToString();
            RewardImage.style.backgroundImage =
                new StyleBackground(ResourceUIHelper.GetResourceImage(
                    QuestManager.DailyCommonRewardType));
            ClaimButton.SetEnabled(
                QuestManager.CanClaimDailyCommonReward);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Восстанавливает композицию и подключает получение общей награды.
        /// </summary>
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

            // Сохраняем защиту выдачи и подключаем callback один раз.
            ClaimButton?.SetEnabled(
                !_claimStarted && QuestManager.CanClaimDailyCommonReward);
            ClaimButton?.UnregisterCallback<ClickEvent>(OnClaimClicked);
            ClaimButton?.RegisterCallback<ClickEvent>(OnClaimClicked);
        }

        /// <summary>
        /// Отключает получение общей награды и возвращает общий modal host.
        /// </summary>
        protected override void OnUnsubscribeFromEvents()
        {
            // Освобождаем действие текущего дерева.
            ClaimButton?.UnregisterCallback<ClickEvent>(OnClaimClicked);

            // Возвращаем общий host следующему окну.
            _presentation?.Restore();
            _presentation = null;
        }

        /// <summary>
        /// Выдаёт одну общую награду; после отказа обновляет доступность кнопки.
        /// </summary>
        private void OnClaimClicked(ClickEvent _)
        {
            // Защищаем очередь до синхронной выдачи и её UI-событий.
            if (_claimStarted)
                return;
            _claimStarted = true;
            ClaimButton?.SetEnabled(false);

            // Успешный показ закрывается один раз, следующая награда ждёт нового показа.
            if (QuestManager.ClaimDailyCommonReward())
            {
                _closeAction.Invoke();
                return;
            }

            // Повторная попытка зависит от актуальной доступности награды.
            _claimStarted = false;
            ClaimButton?.SetEnabled(QuestManager.CanClaimDailyCommonReward);
        }

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
