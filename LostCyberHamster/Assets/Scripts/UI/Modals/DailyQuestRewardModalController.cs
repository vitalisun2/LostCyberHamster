using System;
using System.Threading.Tasks;
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

        private Label Title =>
            _modalContent.Q<Label>("daily-quest-reward-title");

        private Button ClaimButton =>
            _modalContent.Q<Button>("btn_daily_quest_reward_claim");

        private Label ClaimLabel =>
            _modalContent.Q<Label>("daily-quest-reward-claim-label");

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
            _buttonCloseModal.style.display = DisplayStyle.None;
            Title.text = Localize("quests_daily_all_completed");
            ClaimLabel.text = Localize("btn_get");
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
        /// Подключает получение общей награды.
        /// </summary>
        protected override void OnSubscribeToEvents()
        {
            ClaimButton?.RegisterCallback<ClickEvent>(OnClaimClicked);
        }

        /// <summary>
        /// Отключает получение общей награды.
        /// </summary>
        protected override void OnUnsubscribeFromEvents()
        {
            ClaimButton?.UnregisterCallback<ClickEvent>(OnClaimClicked);
        }

        /// <summary>
        /// Выдаёт общую награду и закрывает модальное окно.
        /// </summary>
        private void OnClaimClicked(ClickEvent _)
        {
            if (QuestManager.ClaimDailyCommonReward())
            {
                _closeAction.Invoke();
            }
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
