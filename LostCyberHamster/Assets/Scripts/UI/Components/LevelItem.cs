using System;
using System.Collections.Generic;
using Assets.Scripts.System;
using Extensions;
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

        private readonly List<VisualElement> _starElements = new();
        private bool _templateLoaded;
        private Sprite _fullStarSprite;

        public string LevelName { get; private set; }

        public bool IsLocked { get; private set; }

        public LevelItem()
        {
            EnsureTemplateLoaded();
        }

        public void ConfigureForPart(string partKey, string displayName, string previewAddress, string levelKeyForProgress)
        {
            EnsureTemplateLoaded();
            var resolvedDisplay = TryLocalize(partKey, displayName);
            var resolvedPreview = string.IsNullOrWhiteSpace(previewAddress)
                ? GetDefaultPreviewAddress(partKey)
                : previewAddress;
            SetupCard(resolvedDisplay, resolvedPreview, string.IsNullOrWhiteSpace(levelKeyForProgress) ? levelKeyForProgress : levelKeyForProgress.Trim());
        }
        public void ConfigureForLevel(LevelSelectionModel.LevelReference level, int displayIndex, string partKey, string previewAddress)
        {
            EnsureTemplateLoaded();
            var label = displayIndex > 0 ? displayIndex.ToString() : level.Key;
            var resolvedPreview = string.IsNullOrWhiteSpace(previewAddress)
                ? GetDefaultPreviewAddress(partKey)
                : previewAddress;
            var canonicalLevelKey = string.IsNullOrWhiteSpace(level.Address)
                ? level.Key
                : level.Address.Trim();
            SetupCard(label, resolvedPreview, canonicalLevelKey);
        }


        private void EnsureTemplateLoaded()
        {
            if (_templateLoaded)
            {
                return;
            }

            AddressableExtentions.LoadAssetSync<VisualTreeAsset>("LevelItem.uxml").CloneTree(this);
            _fullStarSprite = AddressableExtentions.LoadAssetSync<Sprite>("star");

            _starElements.Clear();
            if (_starsContainer != null)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var star = _starsContainer.Q<VisualElement>($"star{i}");
                    if (star != null)
                    {
                        _starElements.Add(star);
                    }
                }
            }

            _templateLoaded = true;
        }

        private void SetupCard(string displayName, string previewAddress, string levelKey)
        {
            if (_name != null)
            {
                _name.text = string.IsNullOrWhiteSpace(displayName) ? levelKey : displayName;
            }

            if (_image != null && !string.IsNullOrWhiteSpace(previewAddress))
            {
                try
                {
                    var sprite = AddressableExtentions.LoadAssetSync<Sprite>(previewAddress);
                    if (sprite != null)
                    {
                        _image.style.backgroundImage = new StyleBackground(sprite.texture);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelItem] Failed to load preview '{previewAddress}': {ex.Message}");
                }
            }

            ApplyProgressState(levelKey);
        }

        private void ApplyProgressState(string levelKey)
        {
            LevelName = levelKey;

            if (!string.IsNullOrEmpty(levelKey) && LevelManager.IsLevelOpen(levelKey))
            {
                var stars = LevelManager.GetLevelStars(levelKey);
                for (int i = 0; i < _starElements.Count; i++)
                {
                    if (i < stars && _fullStarSprite != null)
                    {
                        _starElements[i].style.backgroundImage = new StyleBackground(_fullStarSprite.texture);
                    }
                }

                if (_lock != null)
                {
                    _lock.style.display = DisplayStyle.None;
                }

                if (_starsContainer != null)
                {
                    _starsContainer.style.display = DisplayStyle.Flex;
                }

                IsLocked = false;
            }
            else
            {
                if (_lock != null)
                {
                    _lock.style.display = DisplayStyle.Flex;
                }

                if (_starsContainer != null)
                {
                    _starsContainer.style.display = DisplayStyle.None;
                }

                IsLocked = true;
            }
        }

        private static string TryLocalize(string key, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    var localized = LocalizationManager.GetLocalizedString(key);
                    if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, key, StringComparison.Ordinal))
                    {
                        return localized;
                    }
                }
                catch
                {
                }
            }

            return string.IsNullOrWhiteSpace(fallback) ? key : fallback;
        }

        private static string GetDefaultPreviewAddress(string partKey)
        {
            if (string.IsNullOrWhiteSpace(partKey))
            {
                return string.Empty;
            }

            return partKey.ToLowerInvariant() + "_preview";
        }
    }
}

