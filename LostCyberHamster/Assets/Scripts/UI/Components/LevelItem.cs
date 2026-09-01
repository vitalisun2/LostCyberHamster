using System.Collections.Generic;
using Extensions;
using GameManagement.Progress;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class LevelItem : VisualElement
    {
        private static readonly string[] CardStateClasses =
        {
            "level-card--day-part",
            "level-card--level-open",
            "level-card--level-locked",
            "level-card--locked",
            "level-card--unlocked",
            "level-card--morning",
            "level-card--day",
            "level-card--evening",
            "level-card--night"
        };

        private VisualElement CardRoot =>
            this.Q<VisualElement>("level-item");
        private Label NameLabel =>
            this.Q<Label>("level-item__name");
        private VisualElement StarsContainer =>
            this.Q<VisualElement>("level-item__stars-container");

        private readonly List<VisualElement> _starElements = new();
        private bool _templateLoaded;

        public string LevelName { get; private set; }

        public bool IsLocked { get; private set; }

        public LevelItem()
        {
            EnsureTemplateLoaded();
        }

        /// <summary>
        /// Настраивает карточку части суток и её состояние открытия.
        /// </summary>
        public void ConfigureForPart(
            string partKey,
            string displayName,
            bool isUnlocked)
        {
            EnsureTemplateLoaded();
            ResetVisualState();

            AddToClassList("select-time-card-host");
            CardRoot.AddToClassList("level-card--day-part");
            CardRoot.AddToClassList(
                isUnlocked
                    ? "level-card--unlocked"
                    : "level-card--locked");

            string iconClass = ResolvePartIconClass(partKey);
            if (!string.IsNullOrEmpty(iconClass))
            {
                CardRoot.AddToClassList(iconClass);
            }

            NameLabel.text = displayName ?? string.Empty;
            LevelName = partKey ?? string.Empty;
            IsLocked = !isUnlocked;
        }

        /// <summary>
        /// Настраивает карточку существующего уровня по текущему прогрессу.
        /// </summary>
        public void ConfigureForLevel(LevelProgress level, int displayIndex)
        {
            EnsureTemplateLoaded();

            if (level == null)
            {
                ConfigureLockedPlaceholder();
                return;
            }

            ResetVisualState();
            AddToClassList("select-level-card-host");

            string canonicalLevelKey = string.IsNullOrWhiteSpace(level.Address)
                ? level.LevelKey
                : level.Address.Trim();
            LevelName = canonicalLevelKey ?? string.Empty;
            IsLocked = !level.IsUnlocked;

            if (IsLocked)
            {
                CardRoot.AddToClassList("level-card--level-locked");
                CardRoot.AddToClassList("level-card--locked");
                return;
            }

            CardRoot.AddToClassList("level-card--level-open");
            CardRoot.AddToClassList("level-card--unlocked");
            NameLabel.text = displayIndex > 0
                ? displayIndex.ToString()
                : level.LevelKey;
            ApplyStars(level.Stars);
        }

        /// <summary>
        /// Настраивает пустую позицию сетки как закрытый уровень.
        /// </summary>
        public void ConfigureLockedPlaceholder()
        {
            EnsureTemplateLoaded();
            ResetVisualState();

            AddToClassList("select-level-card-host");
            CardRoot.AddToClassList("level-card--level-locked");
            CardRoot.AddToClassList("level-card--locked");
            LevelName = string.Empty;
            IsLocked = true;
        }

        private void EnsureTemplateLoaded()
        {
            if (_templateLoaded)
            {
                return;
            }

            AddressableExtentions
                .LoadAssetSync<VisualTreeAsset>("LevelItem.uxml")
                .CloneTree(this);

            _starElements.Clear();
            if (StarsContainer != null)
            {
                for (int i = 1; i <= 3; i++)
                {
                    VisualElement star = StarsContainer.Q<VisualElement>(
                        $"star{i}");
                    if (star != null)
                    {
                        _starElements.Add(star);
                    }
                }
            }

            _templateLoaded = true;
        }

        private void ResetVisualState()
        {
            RemoveFromClassList("select-time-card-host");
            RemoveFromClassList("select-level-card-host");

            foreach (string className in CardStateClasses)
            {
                CardRoot.RemoveFromClassList(className);
            }

            NameLabel.text = string.Empty;
            foreach (VisualElement star in _starElements)
            {
                star.RemoveFromClassList("level-card__star--earned");
            }
        }

        private void ApplyStars(int stars)
        {
            for (int i = 0; i < _starElements.Count; i++)
            {
                _starElements[i].EnableInClassList(
                    "level-card__star--earned",
                    i < stars);
            }
        }

        private static string ResolvePartIconClass(string partKey)
        {
            return partKey?.ToLowerInvariant() switch
            {
                "morning" => "level-card--morning",
                "afternoon" => "level-card--day",
                "evening" => "level-card--evening",
                "night" => "level-card--night",
                _ => string.Empty
            };
        }
    }
}
