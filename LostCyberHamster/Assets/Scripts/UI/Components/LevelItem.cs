using System;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using Extensions;
using Sirenix.Utilities;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class LevelItem : VisualElement
    {
        private Label _name => this.Q<Label>("level-item__name");
        private VisualElement _image => this.Q<VisualElement>("level-item__image");

        private VisualElement _starsContainer => this.Q<VisualElement>("level-item__stars-container");
        private VisualElement _lock => this.Q<VisualElement>("level-item__lock");

        public string LevelName { get; private set; }

        public bool IsLocked {get; private set;}

        public LevelItem()
        {
        }

        public LevelItem(PartOfDayEnum partOfDay, int locationName)
        {
            AddressableExtentions.LoadAssetSync<VisualTreeAsset>("LevelItem.uxml").CloneTree(this);
            var fullstar = AddressableExtentions.LoadAssetSync<Sprite>("star");
            _name.text = LocalizationManager.GetLocalizedString(partOfDay.ToString());
            var image = AddressableExtentions.LoadAssetSync<Sprite>($"{partOfDay.ToString().ToLower()}_preview");
            _image.style.backgroundImage = new StyleBackground(image.texture);
            LevelName = LevelManager.GetLevelName(locationName, partOfDay);
            if (LevelManager.IsLevelOpen(LevelName))
            {
                var stars = LevelManager.GetLevelStars(LevelName);
                for(int i=1; i<=stars; i++)
                {
                    _starsContainer.Q($"star{i}").style.backgroundImage = fullstar.texture;
                }
                IsLocked = false;
            }
            else
            {
                _lock.style.display = DisplayStyle.Flex;
                _starsContainer.style.display = DisplayStyle.None;
                IsLocked = true;
            }
        }
    }
}