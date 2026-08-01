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
        private VisualElement _imageCircle =>
            this.Q<VisualElement>("quest-item__image-circle");

        private Label _rewardAmount => this.Q<Label>("quest-item__reward-amount");
        private VisualElement _rewardTypeImage => this.Q<VisualElement>("quest-item__reward-image");
        private VisualElement _rewardAction =>
            this.Q<VisualElement>("quest-item__reward-action");

        private Label _title => this.Q<Label>("quest-item__title");

        private Label _progressLabel => this.Q<Label>("quest-item__progress");

        private LocalizedButton _buttonGet => this.Q<LocalizedButton>("quest-item__reward-get");
        private VisualElement _rewardClaimed =>
            this.Q<VisualElement>("quest-item__reward-claimed");

        public QuestItem()
        {
            AddToClassList("quest-card");
        }

        public QuestItem(Quest quest) : this()
        {
            AddressableExtentions
                .LoadAssetSync<VisualTreeAsset>("QuestItem.uxml")
                .CloneTree(this);
            string imageAddress = quest.Category == QuestCategory.Daily
                ? "daily-001"
                : "story-001";
            var image =
                AddressableExtentions.LoadAssetSync<Sprite>(imageAddress);
            _image.style.backgroundImage = new StyleBackground(image.texture);
            ApplyCategoryStyle(quest.Category);
            _title.text = LocalizationManager.GetLocalizedString(
                quest.TitleLocalizationKey) ??
                quest.TitleLocalizationKey;
            _progressLabel.text =
                $"{quest.CurrentProgress} / {quest.TargetAmount}";
            _rewardAmount.text = quest.RewardAmount.ToString();

            var rewardImage =
                ResourceUIHelper.GetResourceImage(quest.RewardType);
            _rewardTypeImage.style.backgroundImage =
                new StyleBackground(rewardImage);

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
            _rewardAction.style.display = isRewardClaimed
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _rewardClaimed.style.display = isRewardClaimed
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // До завершения квеста видимая кнопка остаётся неактивной.
            _buttonGet.SetEnabled(
                !isRewardClaimed &&
                quest.IsCompleted);
        }

        private void ApplyCategoryStyle(QuestCategory category)
        {
            bool isDaily = category == QuestCategory.Daily;
            _buttonGet.style.backgroundColor = isDaily
                ? new StyleColor(new Color32(221, 142, 47, 255))
                : new StyleColor(new Color32(59, 107, 125, 255));
            _imageCircle.style.backgroundColor = isDaily
                ? new StyleColor(new Color32(217, 211, 225, 255))
                : new StyleColor(new Color32(197, 224, 224, 255));
        }
    }
}
