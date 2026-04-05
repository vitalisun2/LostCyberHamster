using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Editor.LevelEditor
{
    /// <summary>
    /// UI panel for managing the pattern sequence of a reference-based level.
    /// Visible only in Location Level mode.
    /// </summary>
    public class PatternSequencePanel
    {
        private readonly VisualElement _root;
        private ListView _availablePatternsList;
        private ListView _sequenceList;
        private TextField _filterField;
        private IntegerField _seedField;
        private Button _addButton;
        private Button _removeButton;
        private Button _randomizeSeedButton;

        private PatternsCollection _patternsCollection;
        private LevelInfoRef _levelRef;
        private List<string> _allPatternNames = new();
        private List<string> _filteredPatternNames = new();
        private int _selectedSequenceIndex = -1;

        public event Action OnSequenceChanged;
        public event Action<int> OnPatternSelected;

        public VisualElement Root => _root;

        public PatternSequencePanel()
        {
            _root = new VisualElement();
            _root.style.display = DisplayStyle.None;
            BuildUI();
        }

        public void Show(LevelInfoRef levelRef, PatternsCollection patterns)
        {
            _levelRef = levelRef;
            _patternsCollection = patterns;
            _allPatternNames = patterns.patterns.Select(p => p.name).ToList();
            _filteredPatternNames = new List<string>(_allPatternNames);
            _root.style.display = DisplayStyle.Flex;
            RefreshUI();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _levelRef = null;
            _patternsCollection = null;
        }

        private void BuildUI()
        {
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.marginTop = 8;
            _root.style.marginBottom = 8;

            var header = new Label("Pattern Sequence");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 13;
            _root.Add(header);

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.height = 200;
            _root.Add(columns);

            // Left column — available patterns
            var leftColumn = new VisualElement();
            leftColumn.style.flexGrow = 1;
            leftColumn.style.flexBasis = 0;
            leftColumn.style.marginRight = 4;
            columns.Add(leftColumn);

            var availableLabel = new Label("Available:");
            leftColumn.Add(availableLabel);

            _filterField = new TextField("Filter");
            _filterField.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
            leftColumn.Add(_filterField);

            _availablePatternsList = new ListView();
            _availablePatternsList.selectionType = SelectionType.Single;
            _availablePatternsList.style.flexGrow = 1;
            _availablePatternsList.makeItem = () => new Label();
            _availablePatternsList.bindItem = (e, i) => ((Label)e).text = _filteredPatternNames[i];
            leftColumn.Add(_availablePatternsList);

            // Right column — level sequence
            var rightColumn = new VisualElement();
            rightColumn.style.flexGrow = 1;
            rightColumn.style.flexBasis = 0;
            rightColumn.style.marginLeft = 4;
            columns.Add(rightColumn);

            var sequenceLabel = new Label("Level Sequence:");
            rightColumn.Add(sequenceLabel);

            _sequenceList = new ListView();
            _sequenceList.selectionType = SelectionType.Single;
            _sequenceList.reorderable = true;
            _sequenceList.style.flexGrow = 1;
            _sequenceList.makeItem = () => new Label();
            _sequenceList.bindItem = (e, i) =>
            {
                if (_levelRef == null || i >= _levelRef.patternSequence.Count) return;
                var patternRef = _levelRef.patternSequence[i];
                var hasOverrides = patternRef.overrides != null && patternRef.overrides.Count > 0;
                ((Label)e).text = $"{i + 1}. {patternRef.@ref}{(hasOverrides ? " (!)" : "")}";
            };
            _sequenceList.selectionChanged += OnSequenceSelectionChanged;
            _sequenceList.itemIndexChanged += OnSequenceReordered;
            rightColumn.Add(_sequenceList);

            // Buttons row
            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            buttonsRow.style.marginTop = 4;
            _root.Add(buttonsRow);

            _addButton = new Button(AddPattern) { text = "+ Add" };
            _removeButton = new Button(RemovePattern) { text = "Remove" };

            buttonsRow.Add(_addButton);
            buttonsRow.Add(_removeButton);

            // Seed row
            var seedRow = new VisualElement();
            seedRow.style.flexDirection = FlexDirection.Row;
            seedRow.style.marginTop = 4;
            _root.Add(seedRow);

            _seedField = new IntegerField("Seed") { value = 0 };
            _seedField.style.flexGrow = 1;
            _seedField.RegisterValueChangedCallback(OnSeedChanged);
            seedRow.Add(_seedField);

            _randomizeSeedButton = new Button(RandomizeSeed) { text = "Randomize" };
            seedRow.Add(_randomizeSeedButton);
        }

        private void RefreshUI()
        {
            _filteredPatternNames = new List<string>(_allPatternNames);
            _availablePatternsList.itemsSource = _filteredPatternNames;
            _availablePatternsList.Rebuild();

            if (_levelRef != null)
            {
                _sequenceList.itemsSource = _levelRef.patternSequence;
                _sequenceList.Rebuild();
            }

            UpdateSeedField();
        }

        private void ApplyFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                _filteredPatternNames = new List<string>(_allPatternNames);
            }
            else
            {
                _filteredPatternNames = _allPatternNames
                    .Where(n => n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            _availablePatternsList.itemsSource = _filteredPatternNames;
            _availablePatternsList.Rebuild();
        }

        private void OnSequenceSelectionChanged(IEnumerable<object> selection)
        {
            _selectedSequenceIndex = _sequenceList.selectedIndex;
            UpdateSeedField();
            if (_selectedSequenceIndex >= 0)
                OnPatternSelected?.Invoke(_selectedSequenceIndex);
        }

        private void UpdateSeedField()
        {
            if (_levelRef != null && _selectedSequenceIndex >= 0 && _selectedSequenceIndex < _levelRef.patternSequence.Count)
            {
                _seedField.SetValueWithoutNotify(_levelRef.patternSequence[_selectedSequenceIndex].spriteSeed);
                _seedField.SetEnabled(true);
            }
            else
            {
                _seedField.SetValueWithoutNotify(0);
                _seedField.SetEnabled(false);
            }
        }

        private void OnSeedChanged(ChangeEvent<int> evt)
        {
            if (_levelRef == null || _selectedSequenceIndex < 0 || _selectedSequenceIndex >= _levelRef.patternSequence.Count)
                return;

            _levelRef.patternSequence[_selectedSequenceIndex].spriteSeed = evt.newValue;
            OnSequenceChanged?.Invoke();
        }

        private void RandomizeSeed()
        {
            if (_levelRef == null || _selectedSequenceIndex < 0 || _selectedSequenceIndex >= _levelRef.patternSequence.Count)
                return;

            _levelRef.patternSequence[_selectedSequenceIndex].spriteSeed = UnityEngine.Random.Range(1, int.MaxValue);
            _seedField.SetValueWithoutNotify(_levelRef.patternSequence[_selectedSequenceIndex].spriteSeed);
            OnSequenceChanged?.Invoke();
        }

        private void AddPattern()
        {
            if (_levelRef == null || _availablePatternsList.selectedIndex < 0)
                return;

            var patternName = _filteredPatternNames[_availablePatternsList.selectedIndex];
            _levelRef.patternSequence.Add(new PatternRef
            {
                @ref = patternName,
                spriteSeed = 0,
                overrides = new List<SpriteOverride>()
            });

            _sequenceList.Rebuild();
            OnSequenceChanged?.Invoke();
        }

        private void RemovePattern()
        {
            if (_levelRef == null || _selectedSequenceIndex < 0 || _selectedSequenceIndex >= _levelRef.patternSequence.Count)
                return;

            _levelRef.patternSequence.RemoveAt(_selectedSequenceIndex);
            _selectedSequenceIndex = Mathf.Min(_selectedSequenceIndex, _levelRef.patternSequence.Count - 1);
            _sequenceList.Rebuild();

            if (_selectedSequenceIndex >= 0)
                _sequenceList.SetSelection(_selectedSequenceIndex);

            OnSequenceChanged?.Invoke();
        }

        private void OnSequenceReordered(int oldIndex, int newIndex)
        {
            if (_levelRef == null) return;

            // ListView already moved the item in itemsSource, just update selection and notify
            _selectedSequenceIndex = newIndex;
            _sequenceList.Rebuild();
            _sequenceList.SetSelection(_selectedSequenceIndex);
            UpdateSeedField();
            OnSequenceChanged?.Invoke();
        }
    }
}
