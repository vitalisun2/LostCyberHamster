using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Editor.LevelEditor.ObstacleSpriteTypeMappingManagement;
using Assets.Scripts;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class LevelTilemapUi
{
    private readonly VisualElement _root;
    private readonly string _opeLocation;
    private DropdownField _locationsDropdownField;
    private DropdownField _obstacleTypeDropdownField;
    private ScrollView _spritesScrollView;
    private ListView _filesList;
    private ListView _patternsList;
    private Button _saveJsonButton;
    private Button _createLevelButton;
    private string _selectedSprite;
    private Toggle _isCollectableOnRoofToggle;
    private Button _resetButton;
    private VisualElement _templateLevelNameParent;// #template-level-name-parent
    private TextField _templateLevelNameField;  // #template-level-name
    private TextField _patternNameField;
    private TextField _patternDescriptionField;
    private TextField _patternSearchField;
    private RadioButtonGroup _daypartRadioGroup;
    private VisualElement _patternsSection;
    private VisualElement _patternButtonsRow;
    private VisualElement _createLevelRow;
    private Label _spritesLabel;
    private AddressableSetLease<Sprite> _obstacleSpritesLease;
    private static string _templatesFallbackLocation => Consts.TemplatesFallbackLocation;
    private readonly PartOfDayEnum[] _daypartOrder = new[]
    {
        PartOfDayEnum.Morning,
        PartOfDayEnum.Afternoon,
        PartOfDayEnum.Evening,
        PartOfDayEnum.Night
    };

    private const string _collectableSpritesTag = "collectable sprites";
    public string TemplateLevelName => _templateLevelNameField?.value;
    public string LevelName => _templateLevelNameField?.value;

    public string CurrentPatternDescription => _patternDescriptionField?.value ?? "";


    public event Action OnCreateLevelClicked;
    public event Action OnSaveLevelClicked;
    public event Action<string> OnLocationChanged;
    public event Action<string> OnSpriteSelected;
    public event Action<LevelFileDescriptor> OnFileSelected;
    public event Action<int> OnPatternSelected;
    public event Action<bool> OnIsCollectableOnRoofToggleChanged;
    public event Action OnResetClicked;
    public event Action<string> OnPatternNameChanged;
    public event Action<string> OnPatternDescriptionChanged;
    public event Action<PartOfDayEnum> OnDaypartChanged;
    public event Action<string> OnPatternSearchChanged;
    public event Action<string> OnLevelNameChanged;

    public LevelTilemapUi(VisualElement root,
        string opeLocation)
    {
        _root = root;
        _opeLocation = opeLocation;

        LoadTemplates();
        SetElements(root);
        InitializeDropdowns();
        InitializeButtons();
        InitializeListViews();
        InitializeToggles();
        InitializeTextFields();
        InitializeDaypartSelector();
        InitializePatternSearch();
    }

    private void SetElements(VisualElement root)
    {
        _createLevelButton = _root.Q<Button>("create-level-btn");
        _locationsDropdownField = _root.Q<DropdownField>("location-dropdown");
        _obstacleTypeDropdownField = root.Q<DropdownField>("obstacle-type-dropdown");
        _spritesScrollView = _root.Q<ScrollView>("sprites");
        _saveJsonButton = _root.Q<Button>("save-btn");
        _filesList = _root.Q<ListView>("files-list-view");
        _patternsList = _root.Q<ListView>("patterns-list-view");
        _isCollectableOnRoofToggle = root.Q<Toggle>("IsCollectableOnRoofToggle");
        _resetButton = _root.Q<Button>("reset-btn");
        _templateLevelNameParent = root.Q<VisualElement>("template-level-name-parent");
        _templateLevelNameField = root.Q<TextField>("template-level-name");
        if (_templateLevelNameParent != null)
        {
            _templateLevelNameParent.style.display = DisplayStyle.None;
        }
        _patternNameField = root.Q<TextField>("selected-pattern-name");
        _patternDescriptionField = root.Q<TextField>("selected-pattern-description");
        _patternSearchField = root.Q<TextField>("pattern-search-field");
        _daypartRadioGroup = root.Q<RadioButtonGroup>("daypart-radio-group");

        // Section containers for mode visibility
        _patternsSection = root.Q<VisualElement>("VisualElement"); // named container with patterns
        _patternButtonsRow = root.Q<Button>("add-pattern-btn")?.parent;
        _createLevelRow = root.Q<Button>("create-level-btn")?.parent;
        _spritesLabel = root.Q<Label>("sprites-label");


    }

    private void LoadTemplates()
    {
        var uiTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{_opeLocation}/LevelTilemapEditor.uxml").Instantiate();
        _root.Add(uiTemplate);

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{_opeLocation}/LevelTilemapEditor.uss");
        _root.styleSheets.Add(styleSheet);
    }

    private void InitializeDropdowns()
    {
        _locationsDropdownField.choices = Directory.GetDirectories(Consts.LocationsPath)
            .Select(d => Path.GetRelativePath(Consts.LocationsPath, d))
            .Where(p => p != "previews")
            .ToList();

        _locationsDropdownField.RegisterValueChangedCallback(v =>
        {
            OnLocationChanged?.Invoke(v.newValue);
        });

        InitializeObstacleTypeDropdown();

    }

    private void InitializeButtons()
    {
        _createLevelButton.clicked += () => OnCreateLevelClicked?.Invoke();
        _saveJsonButton.clicked += () => OnSaveLevelClicked?.Invoke();
        _resetButton.clicked += () => OnResetClicked?.Invoke();
    }

    private void InitializeToggles()
    {
        _isCollectableOnRoofToggle.RegisterValueChangedCallback(evt => OnIsCollectableOnRoofToggleChanged?.Invoke(evt.newValue));
    }

    private void InitializeListViews()
    {
        _filesList.selectionType = SelectionType.Single;
        _filesList.selectionChanged += OnFileSelectedInternal;

        _patternsList.selectionType = SelectionType.Single;
        _patternsList.selectionChanged += OnPatternSelectedInternal;
    }

    private void InitializeTextFields()
    {
        if (_patternNameField != null)
        {
            // Use FocusOut event instead of KeyDownEvent for more reliable handling
            _patternNameField.RegisterCallback<FocusOutEvent>(evt => {
                if (!string.IsNullOrEmpty(_patternNameField.value))
                {
                    Debug.Log($"[UI] Pattern name changed to: {_patternNameField.value}");
                    OnPatternNameChanged?.Invoke(_patternNameField.value);
                }
            });

            // Also keep the Enter key handler for immediate feedback
            _patternNameField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Debug.Log($"[UI] Enter pressed with pattern name: {_patternNameField.value}");
                    // Fire the event when Enter is pressed
                    OnPatternNameChanged?.Invoke(_patternNameField.value);
                    _patternNameField.Blur(); // Remove focus to trigger FocusOut as well
                    evt.StopPropagation(); // Prevent event from bubbling up
                }
            });
        }
        else
        {
            Debug.LogWarning("TextField 'selected-pattern-name' not found in UXML.");
        }

        if (_patternDescriptionField != null)
        {
            _patternDescriptionField.RegisterValueChangedCallback(
                e => OnPatternDescriptionChanged?.Invoke(e.newValue));
        }

        if (_templateLevelNameField != null)
        {
            _templateLevelNameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                OnLevelNameChanged?.Invoke(_templateLevelNameField.value);
            });

            _templateLevelNameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnLevelNameChanged?.Invoke(_templateLevelNameField.value);
                    _templateLevelNameField.Blur();
                    evt.StopPropagation();
                }
            });
        }

    }

    private void InitializeDaypartSelector()
    {
        if (_daypartRadioGroup == null)
        {
            Debug.LogWarning("RadioButtonGroup 'daypart-radio-group' not found in UXML.");
            return;
        }

        _daypartRadioGroup.style.flexDirection = FlexDirection.Row;
        _daypartRadioGroup.style.flexWrap = Wrap.NoWrap;
        _daypartRadioGroup.style.alignItems = Align.Center;
        _daypartRadioGroup.style.justifyContent = Justify.FlexStart;

        var daypartContainer = _daypartRadioGroup.contentContainer;
        daypartContainer.style.flexDirection = FlexDirection.Row;
        daypartContainer.style.flexWrap = Wrap.NoWrap;
        daypartContainer.style.alignItems = Align.Center;
        daypartContainer.style.justifyContent = Justify.FlexStart;

        foreach (var child in _daypartRadioGroup.Children())
        {
            if (child is RadioButton radio)
            {
                radio.style.flexGrow = 0f;
                radio.style.width = StyleKeyword.Auto;
                radio.style.minWidth = StyleKeyword.Auto;
                radio.style.maxWidth = StyleKeyword.Auto;
                radio.style.marginRight = 12f;
                radio.style.marginBottom = 0f;
            }
        }

        _daypartRadioGroup.RegisterValueChangedCallback(evt =>
        {
            var mappedDaypart = GetDaypartForIndex(evt.newValue);
            if (mappedDaypart.HasValue)
            {
                OnDaypartChanged?.Invoke(mappedDaypart.Value);
            }
        });

        SetSelectedDaypart(PartOfDayEnum.Morning);
    }

    private PartOfDayEnum? GetDaypartForIndex(int index)
    {
        if (index < 0 || index >= _daypartOrder.Length)
        {
            return null;
        }

        return _daypartOrder[index];
    }

    private int GetIndexForDaypart(PartOfDayEnum daypart)
    {
        for (int i = 0; i < _daypartOrder.Length; i++)
        {
            if (_daypartOrder[i] == daypart)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnFileSelectedInternal(IEnumerable<object> selectedItems)
    {
        if (selectedItems == null)
        {
            return;
        }

        var selected = selectedItems.FirstOrDefault();
        if (selected is LevelFileDescriptor descriptor)
        {
            OnFileSelected?.Invoke(descriptor);
        }
    }
    private void OnPatternSelectedInternal(IEnumerable<object> selectedItems)
    {
        var item = selectedItems.FirstOrDefault() as int? ?? -1;
        OnPatternSelected?.Invoke(item);
    }

    public void SetObstaclesSpritesListView(string location, string spritesExt)
    {
        ReleaseObstacleSprites();

        _spritesScrollView.Clear();

        var effectiveLocation = ResolveLocationForObstacles(location);
        
        // Load both obstacles and decoration sprites
        var obstacleLabel = BuildObstacleLabel(effectiveLocation);
        var decorLabel = BuildDecorLabel(effectiveLocation);
        
        DebugManager.DiagLog($"[LevelTilemapUi] Loading sprites for location '{effectiveLocation}'. Obstacle label: '{obstacleLabel}', Decor label: '{decorLabel}'");
        
        var allSprites = new List<Sprite>();
        
        // Load obstacles sprites
        if (!string.IsNullOrWhiteSpace(obstacleLabel))
        {
            try
            {
                _obstacleSpritesLease = AddressableLoader.LoadAssetsByLabelSync<Sprite>(obstacleLabel);
                if (_obstacleSpritesLease?.Values != null && _obstacleSpritesLease.Values.Count > 0)
                {
                    allSprites.AddRange(_obstacleSpritesLease.Values);
                    DebugManager.DiagLog($"[LevelTilemapUi] Loaded {_obstacleSpritesLease.Values.Count} obstacle sprites.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelTilemapUi] Ошибка загрузки спрайтов препятствий по лейблу '{obstacleLabel}': {ex.Message}");
            }
        }
        
        // Load decoration sprites
        if (!string.IsNullOrWhiteSpace(decorLabel) && AddressableLoader.HasAssetsForLabel(decorLabel))
        {
            try
            {
                var decorLease = AddressableLoader.LoadAssetsByLabelSync<Sprite>(decorLabel);
                if (decorLease?.Values != null && decorLease.Values.Count > 0)
                {
                    allSprites.AddRange(decorLease.Values);
                    DebugManager.DiagLog($"[LevelTilemapUi] Loaded {decorLease.Values.Count} decoration sprites.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelTilemapUi] Ошибка загрузки декораций по лейблу '{decorLabel}': {ex.Message}");
            }
        }

        if (allSprites.Count == 0)
        {
            Debug.LogWarning($"[LevelTilemapUi] Для локации '{effectiveLocation}' не найдено ни препятствий, ни декораций.");
            ReleaseObstacleSprites();
            return;
        }

        var sprites = allSprites;

        // Группируем sub-sprites по базовому имени (без суффикса кадра -N).
        // Для анимированных спрайт-шитов показываем только первый кадр.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sprite in sprites
                     .OrderBy(sprite => sprite?.name, StringComparer.OrdinalIgnoreCase)
                     .ToList())
        {
            if (sprite == null)
            {
                continue;
            }

            if (!IsObstacleSprite(sprite.name))
            {
                continue;
            }

            var baseName = SpriteLoader.StripFrameSuffix(sprite.name);
            if (!seen.Add(baseName))
            {
                continue; // Уже показали представителя этого спрайт-шита
            }

            // Кешируем спрайт под базовым именем для последующей загрузки в кисть
            SpriteLoader.RegisterSprite(baseName, sprite);

            var container = new Button();
            container.AddToClassList("sprite");

            var spriteImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                sprite = sprite
            };

            container.Add(spriteImage);
            container.RegisterCallback<ClickEvent, string>(OnAssetClick, baseName);
            _spritesScrollView.Add(container);
        }

        _spritesScrollView.MarkDirtyRepaint();
    }

    public void ReleaseObstacleSprites()
    {
        _obstacleSpritesLease?.Dispose();
        _obstacleSpritesLease = null;
    }

    private static string ResolveLocationForObstacles(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return location;
        }

        if (string.Equals(location, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase))
        {
            return _templatesFallbackLocation;
        }

        return location;
    }

    private static string BuildObstacleLabel(string location)
    {
        var normalized = NormalizeLocationForLabel(location);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return $"{normalized} {Consts.ObstaclesSpritesLabelPostfix}";
    }

    private static string BuildDecorLabel(string location)
    {
        var normalized = NormalizeLocationForLabel(location);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return $"{normalized} {Consts.DecorSpritesLabelPostFix}";
    }

    private static bool IsObstacleSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return false;
        }

        return spriteName.StartsWith("obstacle", StringComparison.OrdinalIgnoreCase) ||
               spriteName.StartsWith("decor", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLocationForLabel(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var trimmed = location.Trim().Replace('\\', '/');
        var lastSegment = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrEmpty(lastSegment))
        {
            return null;
        }

        var parts = lastSegment.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return lastSegment.Replace('_', ' ');
        }

        int startIndex = 0;
        if (int.TryParse(parts[0], out _))
        {
            startIndex = 1;
        }

        if (startIndex >= parts.Length)
        {
            return lastSegment.Replace('_', ' ');
        }

        var nameParts = parts.Skip(startIndex);
        var result = string.Join(" ", nameParts);
        return string.IsNullOrWhiteSpace(result) ? lastSegment.Replace('_', ' ') : result;
    }

    public void CreateSpriteImage(string spriteName, VisualElement cell)
    {
        var baseName = Path.GetFileNameWithoutExtension(spriteName);
        var loadedSprite = SpriteLoader.LoadSpriteSync(baseName);

        // Если не загрузился основной спрайт, попробовать первый кадр анимации
        if (loadedSprite == null)
        {
            loadedSprite = SpriteLoader.LoadSpriteSync($"{baseName}_0");
        }

        if (loadedSprite == null)
        {
            Debug.LogError($"Не удалось загрузить спрайт: {baseName}");
            return;
        }

        var spriteImage = new Image
        {
            scaleMode = ScaleMode.ScaleToFit,
            sprite = loadedSprite
        };
        cell.Add(spriteImage);
    }


    public void OnAssetClick(ClickEvent evt, string spriteName)
    {
        var sprites = _root.Query(className: "sprite").ToList();
        foreach (var sprite in sprites)
        {
            sprite.RemoveFromClassList("selected");
        }

        var targetBox = evt.target as VisualElement;

        if (_selectedSprite == spriteName)
        {
            _selectedSprite = null;
            targetBox.RemoveFromClassList("selected");
            OnSpriteSelected?.Invoke(null);
        }
        else
        {
            _selectedSprite = spriteName;
            targetBox.AddToClassList("selected");
            OnSpriteSelected?.Invoke(_selectedSprite);
        }
    }

    private void InitializeObstacleTypeDropdown()
    {
        if (_obstacleTypeDropdownField != null)
        {
            _obstacleTypeDropdownField.choices = new System.Collections.Generic.List<string>
            {
                ObstacleTypeEnum.smallAlive.ToString(),
                ObstacleTypeEnum.bigAlive.ToString(),
                ObstacleTypeEnum.smallNotAliveRoad.ToString(),
                ObstacleTypeEnum.smallNotAliveRoadAndRoof.ToString(),
                ObstacleTypeEnum.bigNotAlive.ToString(),
                ObstacleTypeEnum.collectableEnergetic.ToString(),
                ObstacleTypeEnum.collectablePizza.ToString(),
                ObstacleTypeEnum.collectableCrystal.ToString(),
                ObstacleTypeEnum.collectableLife.ToString(),
                ObstacleTypeEnum.collectableCoin.ToString(),
                ObstacleTypeEnum.decor.ToString()
            };

            _obstacleTypeDropdownField.RegisterValueChangedCallback(evt =>
            {
                if (_selectedSprite != null && System.Enum.TryParse(evt.newValue, out ObstacleTypeEnum newType))
                {
                    ObstacleSpriteTypeMappingsManager.SetType(_selectedSprite, newType);
                    ObstacleSpriteTypeMappingsManager.SaveBindings();
                }
            });
        }
    }

    public void AddCollectablesToSpritesListView()
    {
        // Используем синхронную загрузку для проверки проблемы
        var sprites = SpriteLoader.LoadSpritesSyncByTag(_collectableSpritesTag);

        foreach (var sprite in sprites)
        {
            var container = new Button();
            container.AddToClassList("sprite");

            var spriteImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                sprite = sprite
            };
            container.Add(spriteImage);

            container.RegisterCallback<ClickEvent, string>(OnAssetClick, sprite.name);
            _spritesScrollView.Add(container);
        }

        // Принудительное обновление ScrollView
        _spritesScrollView.MarkDirtyRepaint();
    }

    public void UpdateFilesList(IEnumerable<LevelFileDescriptor> files)
    {
        var items = files?.ToList() ?? new List<LevelFileDescriptor>();

        _filesList.selectionChanged -= OnFileSelectedInternal;
        _filesList.ClearSelection();

        _filesList.itemsSource = items;
        _filesList.RefreshItems();

        _filesList.selectionChanged += OnFileSelectedInternal;
    }

    public void SetSelectedDaypart(PartOfDayEnum daypart)
    {
        if (_daypartRadioGroup == null)
        {
            return;
        }

        var index = GetIndexForDaypart(daypart);
        if (index < 0)
        {
            Debug.LogWarning($"Unsupported daypart '{daypart}' requested for selector.");
            return;
        }

        _daypartRadioGroup.SetValueWithoutNotify(index);
    }

    public void SetDaypartSelectorVisible(bool isVisible)
    {
        if (_daypartRadioGroup == null)
        {
            return;
        }

        _daypartRadioGroup.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetTemplateNameFieldVisible(bool isVisible)
    {
        if (_templateLevelNameParent == null)
        {
            return;
        }

        _templateLevelNameParent.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetFilesListVisible(bool isVisible)
    {
        if (_filesList?.parent == null)
        {
            return;
        }

        // Hide the entire parent container that holds the "files" label, daypart selector, and files list
        _filesList.parent.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetMoveButtonsVisible(bool isVisible)
    {
        var moveUp = _root.Q<Button>("move-up-btn");
        var moveDown = _root.Q<Button>("move-down-btn");
        if (moveUp != null) moveUp.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        if (moveDown != null) moveDown.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Applies UI visibility based on the current editor mode.
    /// Templates mode: shows pattern editing tools, hides file list and level-only elements.
    /// Level mode: shows file list, daypart, hides pattern editing tools.
    /// Sprites palette is visible in both modes (templates for obstacles, levels for decor).
    /// </summary>
    public void ApplyModeUI(bool isTemplateMode)
    {
        // Templates-only elements
        SetVisible(_templateLevelNameParent, true);
        SetVisible(_patternsSection, isTemplateMode);
        SetVisible(_patternButtonsRow, isTemplateMode);
        SetVisible(_obstacleTypeDropdownField, isTemplateMode);
        SetVisible(_isCollectableOnRoofToggle, isTemplateMode);
        SetVisible(_patternSearchField, isTemplateMode);

        // Sprites palette — visible in both modes (templates for obstacles, levels for decor)
        SetVisible(_spritesScrollView, true);
        SetVisible(_spritesLabel, true);

        // Кнопки up/down скрыты в Templates (порядок неважен)
        SetMoveButtonsVisible(!isTemplateMode);

        // Level-only elements
        SetDaypartSelectorVisible(!isTemplateMode);
        SetFilesListVisible(!isTemplateMode);

        // Create button — visible in both modes
        SetVisible(_createLevelRow, true);
    }

    private static void SetVisible(VisualElement element, bool isVisible)
    {
        if (element == null) return;
        element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void InitializePatternSearch()
    {
        if (_patternSearchField == null)
        {
            Debug.LogWarning("TextField 'pattern-search-field' not found in UXML.");
            return;
        }

        _patternSearchField.RegisterValueChangedCallback(evt =>
        {
            OnPatternSearchChanged?.Invoke(evt.newValue);
        });
    }

    public void UpdatePatternsList(List<string> patternNames, int selectedIndex = 0)
    {
        Debug.Log($"[UI] Updating patterns list with {patternNames.Count} patterns, selecting index {selectedIndex}");

        _patternsList.selectionChanged -= OnPatternSelectedInternal;
        _patternsList.ClearSelection();

        var indices = Enumerable.Range(0, patternNames.Count).ToList();
        _patternsList.itemsSource = indices;

        _patternsList.makeItem = () => new Label();
        _patternsList.bindItem = (element, i) => {
            var label = element as Label;
            if (label != null && i >= 0 && i < patternNames.Count)
            {
                label.text = patternNames[i];
            }
            else
            {
                label.text = $"Pattern {i}";
            }
        };

        _patternsList.RefreshItems();

        // Ensure selection happens after refresh
        if (indices.Count > 0)
        {
            int validIndex = Math.Min(selectedIndex, indices.Count - 1);
            Debug.Log($"[UI] Setting pattern selection to index {validIndex}");
            _patternsList.selectedIndex = validIndex;
        }

        // Re-add the event listener after setting selection
        _patternsList.selectionChanged += OnPatternSelectedInternal;
    }

    public void SetObstacleTypeForSelectedSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            _obstacleTypeDropdownField.value = "";
            return;
        }

        if (ObstacleSpriteTypeMappingsManager.TryGetType(spriteName, out var type))
        {
            _obstacleTypeDropdownField.value = type.ToString();
        }
        else
        {
            _obstacleTypeDropdownField.value = "";
        }
    }

    public void SelectFirstFile()
    {
        if (_filesList.itemsSource is List<LevelFileDescriptor> descriptors && descriptors.Count > 0)
        {
            _filesList.selectedIndex = 0;
        }
    }

    public void SelectFileByIndex(int index)
    {
        if (_filesList.itemsSource == null)
        {
            return;
        }

        if (index < 0 || index >= _filesList.itemsSource.Count)
        {
            return;
        }

        _filesList.selectionChanged -= OnFileSelectedInternal;
        _filesList.ClearSelection();
        _filesList.selectionChanged += OnFileSelectedInternal;

        _filesList.selectedIndex = index;
    }

    public void SelectFirstPattern()
    {
        if (_patternsList.itemsSource != null && _patternsList.itemsSource.Count > 0)
        {
            _patternsList.selectedIndex = 0;
            OnPatternSelected?.Invoke(_patternsList.selectedIndex);
        }
    }

    public void SelectPatternByIndex(int index)
    {
        if (_patternsList.itemsSource != null && index >= 0 && index < _patternsList.itemsSource.Count)
        {
            _patternsList.selectedIndex = index;
            OnPatternSelected?.Invoke(index);
        }
    }

    public void UpdatePatternNameField(string patternName)
    {
        if (_patternNameField != null)
        {
            _patternNameField.SetValueWithoutNotify(patternName ?? string.Empty);
        }
    }

    public void UpdatePatternDescriptionField(string desc)
    {
        if (_patternDescriptionField != null)
            _patternDescriptionField.SetValueWithoutNotify(desc ?? "");
    }

    public void UpdateLevelNameField(string levelName)
    {
        if (_templateLevelNameField != null)
            _templateLevelNameField.SetValueWithoutNotify(levelName ?? string.Empty);
    }
}
