using Assets.Scripts.Common.Models;
using Extensions;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class QuestItem : VisualElement
    {
        private VisualElement _image => this.Q<VisualElement>("quest-item__image");

        private Label _rewardAmount => this.Q<Label>("quest-item__reward-amount");
        private VisualElement _rewardTypeImage => this.Q<VisualElement>("quest-item__reward-image");
        private VisualElement _rewardAction =>
            this.Q<VisualElement>("quest-item__reward-action");

        private Label _title => this.Q<Label>("quest-item__title");

        private Label _progressLabel => this.Q<Label>("quest-item__progress");
        private VisualElement _progressFill =>
            this.Q<VisualElement>("quest-item__progress-fill");

        private LocalizedButton _buttonGet => this.Q<LocalizedButton>("quest-item__reward-get");
        private VisualElement _rewardClaimed =>
            this.Q<VisualElement>("quest-item__reward-claimed");

        public QuestItem()
        {
            AddToClassList("quest-card");
        }

        /// <summary>
        /// Создаёт карточку квеста с локализованным названием и аргументами.
        /// </summary>
        public QuestItem(Quest quest) : this()
        {
            // Загружаем шаблон и выбираем подготовленный визуал категории.
            AddressableExtentions
                .LoadAssetSync<VisualTreeAsset>("QuestItem.uxml")
                .CloneTree(this);
            ApplyCategoryStyle(quest.Category);

            // Заполняем название, прогресс и награду квеста.
            _title.text = QuestTitleFormatter.Format(quest);
            _progressLabel.text =
                $"{quest.CurrentProgress}/{quest.TargetAmount}";
            float progress = quest.TargetAmount > 0
                ? Mathf.Clamp01(
                    (float)quest.CurrentProgress / quest.TargetAmount)
                : 0f;
            _progressFill.style.width =
                Length.Percent(progress * 95f);
            _rewardAmount.text = quest.RewardAmount.ToString();

            bool usesPreparedCoin =
                quest.RewardType == ResourceType.Coins;
            _rewardTypeImage.EnableInClassList(
                "quest-reward-icon--coins",
                usesPreparedCoin);
            if (!usesPreparedCoin)
            {
                var rewardImage =
                    ResourceUIHelper.GetResourceImage(quest.RewardType);
                _rewardTypeImage.style.backgroundImage =
                    new StyleBackground(rewardImage);
            }

            // Настраиваем текущее состояние и получение награды.
            UpdateRewardState(quest);

            _buttonGet.RegisterCallback<ClickEvent>(evt =>
            {
                QuestManager.ClaimReward(quest.Id);
            });
        }

        private void UpdateRewardState(Quest quest)
        {
            // После получения награды заменяем CTA постоянным статусом.
            bool isRewardClaimed = quest.IsRewardClaimed;
            bool canClaimReward =
                !isRewardClaimed && quest.IsCompleted;
            _rewardAction.style.display = isRewardClaimed
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _rewardClaimed.style.display = isRewardClaimed
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // До завершения квеста видимая кнопка остаётся неактивной.
            _buttonGet.SetEnabled(canClaimReward);
            _rewardAction.EnableInClassList(
                "quest-reward--disabled",
                !canClaimReward);
        }

        private void ApplyCategoryStyle(QuestCategory category)
        {
            bool isDaily = category == QuestCategory.Daily;
            _image.EnableInClassList("quest-icon--daily", isDaily);
            _image.EnableInClassList("quest-icon--story", !isDaily);
        }
    }
}
