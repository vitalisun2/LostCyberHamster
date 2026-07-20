using System;
using Assets.Scripts.Common.Models;
using Extensions;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Vues.GameCore;

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

        public QuestItem()
        {
        }

        public QuestItem(Quest quest)
        {
            AddressableExtentions.LoadAssetSync<VisualTreeAsset>("QuestItem.uxml").CloneTree(this);
            var image = AddressableExtentions.LoadAssetSync<Sprite>(quest.Id);
            _image.style.backgroundImage = new StyleBackground(image.texture);
            _title.text = quest.Title;
            _progressLabel.text = $"{quest.CurrentAmount} / {quest.TargetAmount}";
            _rewardAmount.text = quest.RewardAmount.ToString();

            var rewardImage =ResourceUIHelper.GetResourceImage(quest.RewardType);
            _rewardTypeImage.style.backgroundImage = new StyleBackground(rewardImage);
            
            _buttonGet.SetEnabled(false);
            if (quest.CurrentAmount >= quest.TargetAmount && !quest.IsRewardRecieved)
            {
                _buttonGet.SetEnabled(true);
            }

            if(quest.IsRewardRecieved)
            {
                _buttonGet.style.display = DisplayStyle.None;
            }

            _buttonGet.RegisterCallback<ClickEvent>(async evt =>
            {
                if (QuestManager.GetReward(quest))
                {
                    _buttonGet.style.display = DisplayStyle.None;
                }
            });
        }
    }
}
