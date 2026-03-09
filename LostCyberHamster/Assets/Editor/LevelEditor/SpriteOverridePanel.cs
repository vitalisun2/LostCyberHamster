using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Editor.LevelEditor
{
    /// <summary>
    /// UI panel for managing sprite overrides on individual obstacles.
    /// Visible only in Location Level mode when an obstacle is selected.
    /// </summary>
    public class SpriteOverridePanel
    {
        private readonly VisualElement _root;
        private Label _patternLabel;
        private Label _obstacleIdLabel;
        private Label _typeLabel;
        private Label _currentSpriteLabel;
        private Label _sourceLabel;
        private ScrollView _spriteListView;
        private Button _resetButton;

        private LevelInfoRef _levelRef;
        private LocationTheme _theme;
        private int _patternIndex = -1;
        private int _obstacleId = -1;
        private int _obstacleType = -1;

        public event Action OnOverrideChanged;

        public SpriteOverridePanel(VisualElement parent)
        {
            _root = new VisualElement();
            _root.style.display = DisplayStyle.None;
            BuildUI();
            parent.Add(_root);
        }

        public void Show(LevelInfoRef levelRef, LocationTheme theme, int patternIndex, ObstacleSlot slot, string currentSpriteName, string source)
        {
            _levelRef = levelRef;
            _theme = theme;
            _patternIndex = patternIndex;
            _obstacleId = slot.id;
            _obstacleType = slot.type;
            _root.style.display = DisplayStyle.Flex;

            _patternLabel.text = $"Pattern: {levelRef.patternSequence[patternIndex].@ref}";
            _obstacleIdLabel.text = $"Obstacle ID: {slot.id}";
            _typeLabel.text = $"Type: {(ObstacleTypeEnum)slot.type} ({slot.type})";
            _currentSpriteLabel.text = $"Current sprite: {currentSpriteName}";
            _sourceLabel.text = $"Source: [{source}]";

            PopulateSpriteList(slot.type, currentSpriteName);
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _levelRef = null;
            _patternIndex = -1;
            _obstacleId = -1;
        }

        private void BuildUI()
        {
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.marginTop = 8;
            _root.style.paddingTop = 4;
            _root.style.paddingBottom = 4;
            _root.style.paddingLeft = 4;
            _root.style.paddingRight = 4;
            _root.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

            var header = new Label("Obstacle Override");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 13;
            _root.Add(header);

            _patternLabel = new Label();
            _root.Add(_patternLabel);

            _obstacleIdLabel = new Label();
            _root.Add(_obstacleIdLabel);

            _typeLabel = new Label();
            _root.Add(_typeLabel);

            _currentSpriteLabel = new Label();
            _root.Add(_currentSpriteLabel);

            _sourceLabel = new Label();
            _root.Add(_sourceLabel);

            var spritesHeader = new Label("Available sprites:");
            spritesHeader.style.marginTop = 6;
            _root.Add(spritesHeader);

            _spriteListView = new ScrollView();
            _spriteListView.style.maxHeight = 150;
            _root.Add(_spriteListView);

            _resetButton = new Button(ResetOverride) { text = "Reset to default" };
            _resetButton.style.marginTop = 4;
            _root.Add(_resetButton);
        }

        private void PopulateSpriteList(int obstacleType, string currentSpriteName)
        {
            _spriteListView.Clear();

            if (_theme == null) return;

            var mapping = _theme.obstacle_sprite_to_type_mappings
                .FirstOrDefault(m => m.type == obstacleType);

            if (mapping == null) return;

            foreach (var spriteName in mapping.sprites)
            {
                var isSelected = string.Equals(spriteName, currentSpriteName, StringComparison.OrdinalIgnoreCase);
                var isDefault = string.Equals(spriteName, mapping.@default, StringComparison.OrdinalIgnoreCase);

                var label = isSelected ? ">" : " ";
                var suffix = isDefault ? " (default)" : "";
                var btn = new Button(() => SelectSprite(spriteName))
                {
                    text = $"{label} {spriteName}{suffix}"
                };

                if (isSelected)
                    btn.style.backgroundColor = new Color(0.3f, 0.5f, 0.3f, 1f);

                _spriteListView.Add(btn);
            }
        }

        private void SelectSprite(string spriteName)
        {
            if (_levelRef == null || _patternIndex < 0 || _patternIndex >= _levelRef.patternSequence.Count)
                return;

            var patternRef = _levelRef.patternSequence[_patternIndex];
            if (patternRef.overrides == null)
                patternRef.overrides = new List<SpriteOverride>();

            var existing = patternRef.overrides.FirstOrDefault(o => o.obstacleId == _obstacleId);
            if (existing != null)
            {
                existing.spriteName = spriteName;
            }
            else
            {
                patternRef.overrides.Add(new SpriteOverride
                {
                    obstacleId = _obstacleId,
                    spriteName = spriteName
                });
            }

            _currentSpriteLabel.text = $"Current sprite: {spriteName}";
            _sourceLabel.text = "Source: [Manual override]";
            PopulateSpriteList(_obstacleType, spriteName);
            OnOverrideChanged?.Invoke();
        }

        private void ResetOverride()
        {
            if (_levelRef == null || _patternIndex < 0 || _patternIndex >= _levelRef.patternSequence.Count)
                return;

            var patternRef = _levelRef.patternSequence[_patternIndex];
            patternRef.overrides?.RemoveAll(o => o.obstacleId == _obstacleId);

            _sourceLabel.text = "Source: [Theme default]";
            OnOverrideChanged?.Invoke();
        }
    }
}
