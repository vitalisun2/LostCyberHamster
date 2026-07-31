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

        private Label _title => this.Q<Label>("quest-item__title");

        private Label _progressLabel => this.Q<Label>("quest-item__progress");

        private LocalizedButton _buttonGet => this.Q<LocalizedButton>("quest-item__reward-get");
        private VisualElement _rewardClaimed =>
            this.Q<VisualElement>("quest-item__reward-claimed");

        public QuestItem()
        {
        }

        public QuestItem(Quest quest)
        {
            AddressableExtentions
                .LoadAssetSync<VisualTreeAsset>("QuestItem.uxml")
                .CloneTree(this);
            var image =
                AddressableExtentions.LoadAssetSync<Sprite>(quest.Id);
            _image.style.backgroundImage = new StyleBackground(image.texture);
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
            // После получения награды заменяем кнопку постоянным статусом.
            bool isRewardClaimed = quest.IsRewardClaimed;
            _buttonGet.style.display = isRewardClaimed
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
    }
}
