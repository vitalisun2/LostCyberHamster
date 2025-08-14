using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Editor.LevelEditor.ObstacleSpriteTypeMappingManagement;
using Assets.Scripts;
using Assets.Scripts.Common.Models;
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
    private FloatField _patternDurationField;
    private ListView _patternsList;
    private Button _saveJsonButton;
    private Button _createLevelButton;
    private string _selectedSprite;
    private DropdownField _backGroundDropdown;
    private Toggle _isCollectableOnRoofToggle;
    private Button _resetButton;
    private Toggle _templateModeToggle;      // #template-mode-toggle
    private VisualElement _templateLevelNameParent;// #template-level-name-parent
    private TextField _templateLevelNameField;  // #template-level-name
    private TextField _patternNameField;
    private TextField _patternDescriptionField;

    public DropdownField BackGroundDropdown => _backGroundDropdown;
    private const string _collectableSpritesTag = "collectable sprites";
    public bool IsTemplateMode => _templateModeToggle?.value ?? false;
    public string TemplateLevelName => _templateLevelNameField?.value;

    public string CurrentPatternDescription => _patternDescriptionField?.value ?? "";


    public event Action OnCreateLevelClicked;
    public event Action OnSaveLevelClicked;
    public event Action<string> OnLocationChanged;
    public event Action<string> OnSpriteSelected;
    public event Action<string> OnFileSelected;
    public event Action<int> OnPatternSelected;
    public event Action<bool> OnIsCollectableOnRoofToggleChanged;
    public event Action OnResetClicked;
    public event Action<string> OnBackgroundSelected;
    public event Action<float> OnPatternDurationChanged;
    public event Action<string> OnPatternNameChanged;
    public event Action<string> OnPatternDescriptionChanged;

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
        InitializeFloatFields();
        InitializeTextFields();
    }

    private void SetElements(VisualElement root)
    {
        _backGroundDropdown = root.Q<DropdownField>("background-dropdown");
        _createLevelButton = _root.Q<Button>("create-level-btn");
        _locationsDropdownField = _root.Q<DropdownField>("location-dropdown");
        _obstacleTypeDropdownField = root.Q<DropdownField>("obstacle-type-dropdown");
        _spritesScrollView = _root.Q<ScrollView>("sprites");
        _saveJsonButton = _root.Q<Button>("save-btn");
        _filesList = _root.Q<ListView>("files-list-view");
        _patternsList = _root.Q<ListView>("patterns-list-view");
        _isCollectableOnRoofToggle = root.Q<Toggle>("IsCollectableOnRoofToggle");
        _resetButton = _root.Q<Button>("reset-btn");
        _patternDurationField = root.Q<FloatField>("patternDuration");
        _templateModeToggle = root.Q<Toggle>("template-mode-toggle");
        _templateLevelNameParent = root.Q<VisualElement>("template-level-name-parent");
        _templateLevelNameField = root.Q<TextField>("template-level-name");
        _patternNameField = root.Q<TextField>("selected-pattern-name");
        _patternDescriptionField = root.Q<TextField>("selected-pattern-description");


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

        _backGroundDropdown.RegisterValueChangedCallback(evt =>
        {
            OnBackgroundSelected?.Invoke(evt.newValue);
        });

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
        _templateModeToggle.RegisterValueChangedCallback(OnTemplateModeToggleChangedInternal);
    }

    private void InitializeListViews()
    {
        _filesList.selectionType = SelectionType.Single;
        _filesList.selectionChanged += OnFileSelectedInternal;

        _patternsList.selectionType = SelectionType.Single;
        _patternsList.selectionChanged += OnPatternSelectedInternal;
    }

    private void InitializeFloatFields()
    {
        if (_patternDurationField != null)
        {
            // При изменении значения оповещаем EditorWindow
            _patternDurationField.RegisterValueChangedCallback(evt =>
            {
                OnPatternDurationChanged?.Invoke(evt.newValue);
            });
        }
        else
        {
            Debug.LogWarning("FloatField 'patternDuration' не найден в UXML.");
        }
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

    }

    private void OnFileSelectedInternal(IEnumerable<object> items)
    {
        var file = items.FirstOrDefault() as string;
        OnFileSelected?.Invoke(file);
    }

    private void OnPatternSelectedInternal(IEnumerable<object> selectedItems)
    {
        var item = selectedItems.FirstOrDefault() as int? ?? -1;
        OnPatternSelected?.Invoke(item);
    }

    private void OnTemplateModeToggleChangedInternal(ChangeEvent<bool> evt)
    {
        bool isTemplate = evt.newValue;

        // Скрыть/показать поле имени уровня через родительский контейнер
        _templateLevelNameParent.style.display = isTemplate ? DisplayStyle.Flex : DisplayStyle.None;
    }


    public void SetObstaclesSpritesListView(string spritePath, string spritesExt)
    {
        var spriteNames = GetFolderFilenames(spritePath, "Sprites", spritesExt);
        _spritesScrollView.Clear();

        foreach (var spriteName in spriteNames)
        {
            if (Path.GetFileNameWithoutExtension(spriteName).StartsWith("obstacle") || Path.GetFileNameWithoutExtension(spriteName).StartsWith("decor"))
            {
                var container = new Button();
                container.AddToClassList("sprite");
                CreateSpriteImage(spriteName, container);
                container.RegisterCallback<ClickEvent, string>(OnAssetClick, spriteName);
                _spritesScrollView.Add(container);
            }
        }
    }

    private List<string> GetFolderFilenames(string folderPath, string folderName, string extension)
    {
        return Directory.GetFiles(Path.Combine(Consts.LocationsPath, folderPath, folderName))
            .Where(s => Path.GetExtension(s).TrimStart('.').ToLowerInvariant() == extension)
            .ToList();
    }

    public void CreateSpriteImage(string spriteName, VisualElement cell)
    {
        var baseName = Path.GetFileNameWithoutExtension(spriteName);
        var loadedSprite = SpriteLoader.LoadSpriteSync(baseName);

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

    public void UpdateFilesList(IEnumerable<string> files)
    {
        _filesList.itemsSource = files.ToList();
        _filesList.RefreshItems();
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
        if (_filesList.itemsSource != null && _filesList.itemsSource.Count > 0)
        {
            _filesList.selectedIndex = 0;
            OnFileSelected?.Invoke((_filesList.itemsSource as List<string>)?[0]);
        }
    }

    public void SelectFirstPattern()
    {
        if (_patternsList.itemsSource != null && _patternsList.itemsSource.Count > 0)
        {
            _patternsList.selectedIndex = 0;
            OnPatternSelected?.Invoke(_patternsList.selectedIndex);
        }
    }

    public void UpdatePatternNameField(string patternName)
    {
        if (_patternNameField != null)
        {
            _patternNameField.value = patternName;
        }
    }

    public void UpdatePatternDescriptionField(string desc)
    {
        if (_patternDescriptionField != null)
            _patternDescriptionField.value = desc ?? "";
    }
}
