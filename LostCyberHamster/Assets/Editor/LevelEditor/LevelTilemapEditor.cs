using Assets.Editor.LevelEditor;
using Assets.Scripts.Common.Models;
using Assets.Scripts;
using Assets.Scripts.System;
using Assets.Scripts.System.LevelManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Unity.VisualScripting;

public class LevelTilemapEditor : EditorWindow
{
    /// <summary>
    /// Стандартная ширина тайлмапа.
    /// </summary>
    private const int DefaultTilemapWidth = 40;

    /// <summary>
    /// Расширение файлов уровня.
    /// </summary>
    private const string _levelsExt = "json";

    /// <summary>
    /// Расширение спрайтовых файлов.
    /// </summary>
    private const string _spritesExt = "png";

    /// <summary>
    /// Путь к расположению UI-элементов.
    /// </summary>
    private const string _opeLocation = "Assets/Editor/LevelEditor";

    /// <summary>
    /// Длительность паттерна в минутах.
    /// </summary>
    private float _patternDurationMinutes = 1f;

    private LevelTilemapUi _uiManager;
    private LevelInfo _currentLevelInfo;
    private LevelInfoRef _currentLevelRef;
    private PatternsCollection _patternsCollection;
    private LocationTheme _locationTheme;
    private string _selectedFile;
    private int _selectedPatternIndex = -1;
    private Tilemap _tipeMapInScene;
    private Pattern _currentPattern;
    private bool _isCollectableOnRoof;
    private bool _isTilemapBulkOperation;
    private PartOfDayEnum _selectedDaypart = PartOfDayEnum.Morning;
    private List<LevelFileDescriptor> _allLevelDescriptors = new();
    private List<LevelFileDescriptor> _visibleLevelDescriptors = new();
    private LevelFileDescriptor? _selectedLevelDescriptor;

    private PatternSequencePanel _patternSequencePanel;
    private SpriteOverridePanel _spriteOverridePanel;

    private string _levelsDirectory;
    private string _levelDesignTemplatesDirectory;
    private string _spritesDirectory;
    private string _currentLocationName;
    private List<string> _spritesNames { get; set; }
    
    // Scene management for non-intrusive workflow
    private string _originalScenePath;
    private List<GameObject> _hiddenRootObjects = new();

    /// <summary>
    /// Открывает окно редактора LevelTilemapEditor.
    /// </summary>
    [MenuItem("Tools/Level Tilemap Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelTilemapEditor>("Level Tilemap Editor");
    }

    /// <summary>
    /// Инициализация UI и подписка на события.
    /// </summary>
    public void CreateGUI()
    {
        _uiManager = new LevelTilemapUi(rootVisualElement, _opeLocation);
        _patternSequencePanel = new PatternSequencePanel(rootVisualElement);
        _spriteOverridePanel = new SpriteOverridePanel(rootVisualElement);
        SubscribeEvents();
        InitializePatternButtons();
        InitializeLevelDesignTemplateDirectory();
    }

    private void OnEnable()
    {
        // Save current scene path for restoration
        var currentScene = SceneManager.GetActiveScene();
        _originalScenePath = currentScene.path;
        
        // Hide all existing root objects in the scene
        _hiddenRootObjects.Clear();
        foreach (var rootObject in currentScene.GetRootGameObjects())
        {
            if (rootObject.activeSelf)
            {
                rootObject.SetActive(false);
                _hiddenRootObjects.Add(rootObject);
            }
        }
    }

    private void OnDisable()
    {
        _uiManager?.ReleaseObstacleSprites();
        UnsubscribeEvents();
        
        // Don't restore scene during assembly reload or domain reload
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }
        
        // Restore scene by reloading without saving changes
        if (!string.IsNullOrEmpty(_originalScenePath))
        {
            EditorSceneManager.OpenScene(_originalScenePath, OpenSceneMode.Single);
            Debug.Log($"[LevelTilemapEditor] Scene restored: {_originalScenePath}");
        }
        else
        {
            // If no scene was open, restore visibility of hidden objects
            foreach (var obj in _hiddenRootObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        _hiddenRootObjects.Clear();
    }

    /// <summary>
    /// Обработчик изменения тайлов в тайлмапе.
    /// </summary>
    private void OnTileChanged(Tilemap changedTilemap, Tilemap.SyncTile[] changes)
    {
        Tilemap.tilemapTileChanged -= OnTileChanged;

        if (_isTilemapBulkOperation)
        {
            Tilemap.tilemapTileChanged += OnTileChanged;
            return;
        }

        if (changedTilemap != _tipeMapInScene)
        {
            Tilemap.tilemapTileChanged += OnTileChanged;
            return;
        }

        foreach (var change in changes)
        {
            var tile = changedTilemap.GetTile<Tile>(change.position);
            if (tile == null || tile.sprite == null)
            {
                Debug.LogWarning($"Tile removed or invalid at {change.position}");
                continue;
            }

            ProcessTileChange(changedTilemap, tile, change.position);
        }

        UpdateCurrentLevelInfoFromTilemap();
        Tilemap.tilemapTileChanged += OnTileChanged;
    }

    /// <summary>
    /// Логика обработки одного изменённого тайла.
    /// </summary>
    private void ProcessTileChange(Tilemap changedTilemap, Tile tile, Vector3Int cellPosition)
    {
        var isTemplateLocation = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);
        
        // For decoration sprites (starting with "decor"), allow placement only in non-template locations
        if (tile.sprite.name.StartsWith("decor", StringComparison.OrdinalIgnoreCase))
        {
            if (isTemplateLocation)
            {
                // Decorations not allowed in Templates
                changedTilemap.SetTile(cellPosition, null);
                Debug.LogWarning("Decorations cannot be placed in Templates. Use specific locations (New York, Paris, etc.)");
                return;
            }
            return; // Decorations can be placed freely in locations
        }

        // For obstacle sprites in specific locations (not templates), block editing
        if (!isTemplateLocation)
        {
            // Obstacles are read-only in specific locations
            changedTilemap.SetTile(cellPosition, null);
            Debug.LogWarning("Obstacles are read-only in specific locations. Edit them in Templates mode.");
            return;
        }

        // For obstacle sprites in Templates, apply placement rules
        if (!ObstacleSpriteTypeMappingsManager.TryGetType(tile.sprite.name, out var obstacleType))
            throw new InvalidOperationException($"No mapping found for sprite '{tile.sprite.name}'");

        var strategy = TilePlacementStrategies.GetStrategyForType(obstacleType, _isCollectableOnRoof);
        var initialWorldPos = changedTilemap.CellToWorld(cellPosition);

        if (!strategy.TryPlaceTile(changedTilemap, tile, initialWorldPos, out var finalWorldPos))
        {
            changedTilemap.SetTile(cellPosition, null);
            Debug.LogWarning($"Tile '{tile.sprite.name}' could not be placed according to the rules and was removed.");
            return;
        }

        if (finalWorldPos != initialWorldPos)
        {
            changedTilemap.SetTile(cellPosition, null);
            var finalCellPos = changedTilemap.WorldToCell(finalWorldPos);
            changedTilemap.SetTile(finalCellPos, tile);
        }
    }

    /// <summary>
    /// Обновляет информацию об уровне, основываясь на содержимом тайлмапа.
    /// </summary>
    private void UpdateCurrentLevelInfoFromTilemap()
    {
        if (_tipeMapInScene == null || _currentLevelInfo == null)
        {
            Debug.LogWarning("Tilemap или CurrentLevelInfo не инициализированы.");
            return;
        }

        // For specific locations (not templates), decorations are synced only on save
        var isTemplateLocation = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);
        if (!isTemplateLocation)
        {
            return;
        }

        // Templates mode: update patterns
        if (_selectedPatternIndex < 0 || _selectedPatternIndex >= _currentLevelInfo.patterns.Count)
        {
            Debug.LogWarning("Выбранный паттерн некорректен.");
            return;
        }

        var selectedPattern = _currentLevelInfo.patterns[_selectedPatternIndex];
        var updatedObstacles = new List<ObstacleModel>();

        foreach (var cellPos in _tipeMapInScene.cellBounds.allPositionsWithin)
        {
            var tile = _tipeMapInScene.GetTile(cellPos) as Tile;
            if (tile == null || tile.sprite == null)
                continue;

            updatedObstacles.Add(CreateObstacleModelFromCell(_tipeMapInScene, cellPos, tile));
        }

        selectedPattern.obstacles = updatedObstacles;
        _currentLevelInfo.patterns[_selectedPatternIndex] = selectedPattern;
    }

    /// <summary>
    /// Создаёт модель препятствия из клетки тайлмапа.
    /// </summary>
    private ObstacleModel CreateObstacleModelFromCell(Tilemap tilemap, Vector3Int cellPos, Tile tile)
    {
        var worldPos = tilemap.GetCellCenterWorld(cellPos);
        return new ObstacleModel
        {
            spriteName = tile.name,
            x = worldPos.x,
            y = worldPos.y,
            type = GetObstacleTypeFromSprite(tile.name)
        };
    }

    /// <summary>
    /// Определяет тип препятствия по имени спрайта.
    /// </summary>
    private int GetObstacleTypeFromSprite(string spriteName)
    {
        if (ObstacleSpriteTypeMappingsManager.TryGetType(spriteName, out var obstacleType))
        {
            return (int)obstacleType;
        }

        Debug.LogWarning($"Не удалось определить тип для спрайта {spriteName}. Используется тип по умолчанию.");
        return 0;
    }

    /// <summary>
    /// Синхронизирует decoration спрайты с Tilemap в decorationPatterns.
    /// </summary>
    private void SyncDecorationsFromTilemap()
    {
        if (_tipeMapInScene == null || _currentLevelInfo == null)
        {
            Debug.LogWarning("[LevelTilemapEditor] Tilemap или CurrentLevelInfo не инициализированы.");
            return;
        }

        var decorationTiles = new List<DecorationTile>();

        foreach (var cellPos in _tipeMapInScene.cellBounds.allPositionsWithin)
        {
            var tile = _tipeMapInScene.GetTile(cellPos) as Tile;
            if (tile == null || tile.sprite == null)
                continue;

            // Only save decoration sprites (starting with "decor")
            if (!tile.name.StartsWith("decor", StringComparison.OrdinalIgnoreCase))
                continue;

            decorationTiles.Add(new DecorationTile
            {
                name = tile.name,
                xPos = cellPos.x,
                yPos = cellPos.y
            });
        }

        // Initialize decorationPatterns if null
        if (_currentLevelInfo.decorationPatterns == null)
        {
            _currentLevelInfo.decorationPatterns = new List<DecorationPattern>();
        }

        // Clear existing and add new pattern
        _currentLevelInfo.decorationPatterns.Clear();
        _currentLevelInfo.decorationPatterns.Add(new DecorationPattern
        {
            decorationTiles = decorationTiles
        });

        DebugManager.DiagLog($"[LevelTilemapEditor] Synced {decorationTiles.Count} decoration tiles to decorationPatterns.");
    }

    /// <summary>
    /// Загружает decoration спрайты из decorationPatterns на Tilemap.
    /// Decorations загружаются ПОВЕРХ obstacles (не очищая Tilemap).
    /// </summary>
    private void LoadDecorationsToTilemap()
    {
        if (_tipeMapInScene == null || _currentLevelInfo == null)
        {
            Debug.LogWarning("[LevelTilemapEditor] Tilemap или CurrentLevelInfo не инициализированы.");
            return;
        }

        // DON'T clear tilemap - obstacles already loaded!
        // Decorations are placed on top of obstacles

        // Initialize decorationPatterns if missing
        if (_currentLevelInfo.decorationPatterns == null)
        {
            _currentLevelInfo.decorationPatterns = new List<DecorationPattern>();
        }

        if (_currentLevelInfo.decorationPatterns.Count == 0)
        {
            Debug.Log("[LevelTilemapEditor] No decoration patterns found. Ready for decoration placement on top of obstacles.");
            return;
        }

        var decorationPattern = _currentLevelInfo.decorationPatterns[0];
        if (decorationPattern.decorationTiles == null || decorationPattern.decorationTiles.Count == 0)
        {
            Debug.Log("[LevelTilemapEditor] Decoration pattern has no tiles. Tilemap is empty.");
            return;
        }

        int loadedCount = 0;
        foreach (var decorTile in decorationPattern.decorationTiles)
        {
            var sprite = SpriteLoader.LoadSpriteSync(decorTile.name);
            if (sprite == null)
            {
                Debug.LogWarning($"[LevelTilemapEditor] Failed to load decoration sprite: {decorTile.name}");
                continue;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = decorTile.name;

            var cellPos = new Vector3Int(decorTile.xPos, decorTile.yPos, 0);
            _tipeMapInScene.SetTile(cellPos, tile);
            loadedCount++;
        }

        DebugManager.DiagLog($"[LevelTilemapEditor] Loaded {loadedCount} decoration tiles to Tilemap.");
    }

    /// <summary>
    /// Сохраняет текущий уровень.
    /// </summary>
    private void HandleSaveLevelClicked()
    {
        var isTemplateLocation = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);
        
        if (isTemplateLocation)
        {
            Debug.Log($"Сохранение PatternsCollection: {_selectedFile}");
            SyncTemplatesFromLevelInfo();
            var pcPath = Path.Combine(Consts.LocationsPath, Consts.TemplatesLocationName, "levels", "PatternsCollection.json");
            var json = JsonUtility.ToJson(_patternsCollection, true);
            File.WriteAllText(pcPath, json, System.Text.Encoding.UTF8);
        }
        else
        {
            Debug.Log($"Сохранение уровня (ref) для локации {_currentLocationName}: {_selectedFile}");
            SyncDecorationsFromTilemap();

            if (_currentLevelRef != null)
            {
                _currentLevelRef.decorationPatterns = _currentLevelInfo.decorationPatterns;
                LevelDataManager.SaveLevelRef(_currentLevelRef, _selectedFile);
            }
            else
            {
                LevelDataManager.SaveLevel(_currentLevelInfo, _selectedFile);
            }
        }
    }

    /// <summary>
    /// Создаёт новый уровень с выбранным фоном.
    /// </summary>
    private void HandleCreateLevelClicked()
    {
        var newLevelInfo = new LevelInfo();

        string createdLevelPath = null;
        var isTemplateLocation = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);

        if (isTemplateLocation)
        {
            var templateName = _uiManager.TemplateLevelName;
            if (string.IsNullOrWhiteSpace(templateName))
            {
                EditorUtility.DisplayDialog("Template name required", "Введите имя шаблона перед созданием файла.", "OK");
                return;
            }

            createdLevelPath = LevelDataManager.CreateNewTemplate(newLevelInfo, templateName, _levelsDirectory, _spritesNames);
        }
        else
        {
            createdLevelPath = LevelDataManager.CreateNewLevel(newLevelInfo, _levelsDirectory, _spritesNames);
        }

       
        AssetDatabase.Refresh();
        RefreshLevelFilesList(reloadFromDisk: true, autoSelectFirst: false);

        if (!string.IsNullOrEmpty(createdLevelPath))
        {
            var index = _visibleLevelDescriptors.FindIndex(d => string.Equals(d.AbsolutePath, createdLevelPath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _uiManager.SelectFileByIndex(index);
            }
        }
    }

    /// <summary>
    /// Реакция на изменение локации в UI.
    /// </summary>
    private void HandleLocationChanged(string newValue)
    {
    _uiManager.SetObstaclesSpritesListView(newValue, _spritesExt);
        _uiManager.AddCollectablesToSpritesListView();

        /* Загружаем маппинг спрайт‑типов для выбранной локации */
        ObstacleSpriteTypeMappingsManager.LoadBindings(newValue, success =>
        {
            if (!success)
                Debug.LogWarning($"No mapping file yet for '{newValue}'. It will be created on first save.");
        });

        _currentLocationName = newValue;

        var isTemplateLocation = string.Equals(newValue, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);
        if (isTemplateLocation)
        {
            _uiManager.SetTemplateNameFieldVisible(true);
            _uiManager.SetDaypartSelectorVisible(false);
            _uiManager.SetFilesListVisible(false);
            _uiManager.SetMoveButtonsVisible(false);
        }
        else
        {
            _selectedDaypart = PartOfDayEnum.Morning;
            _uiManager.SetTemplateNameFieldVisible(false);
            _uiManager.SetDaypartSelectorVisible(true);
            _uiManager.SetSelectedDaypart(_selectedDaypart);
            _uiManager.SetFilesListVisible(true);
            _uiManager.SetMoveButtonsVisible(true);
        }

        _selectedLevelDescriptor = null;

        _levelsDirectory = Path.Combine(Consts.LocationsPath, newValue, "levels");
        UpdateSpritesInfoInCurrentLocation(newValue);

        if (isTemplateLocation)
        {
            LoadTemplatesDirectly();
        }
        else
        {
            RefreshLevelFilesList(reloadFromDisk: true, autoSelectFirst: true);
        }
    }

    private void HandleDaypartChanged(PartOfDayEnum newDaypart)
    {
        if (_selectedDaypart == newDaypart)
        {
            return;
        }

        _selectedDaypart = newDaypart;
        _selectedLevelDescriptor = null;

        RefreshLevelFilesList(reloadFromDisk: false, autoSelectFirst: true);
    }

    private void RefreshLevelFilesList(bool reloadFromDisk = false, bool autoSelectFirst = false)
    {
        if (string.IsNullOrEmpty(_levelsDirectory))
        {
            _allLevelDescriptors.Clear();
            _visibleLevelDescriptors.Clear();
            _uiManager.UpdateFilesList(_visibleLevelDescriptors);
            ClearScene();
            return;
        }

        if (reloadFromDisk || _allLevelDescriptors.Count == 0)
        {
            _allLevelDescriptors = LevelDataManager
                .GetLevelFileDescriptors(_levelsDirectory, _levelsExt)
                .ToList();
        }

        var isTemplateLocation = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);

        _visibleLevelDescriptors = isTemplateLocation
            ? new List<LevelFileDescriptor>(_allLevelDescriptors)
            : _allLevelDescriptors
                .Where(descriptor => descriptor.PartOfDay.HasValue && descriptor.PartOfDay.Value == _selectedDaypart)
                .ToList();

        _uiManager.UpdateFilesList(_visibleLevelDescriptors);

        if (_visibleLevelDescriptors.Count == 0)
        {
            ClearScene();
            return;
        }

        if (autoSelectFirst)
        {
            _uiManager.SelectFirstFile();
            return;
        }

        if (_selectedLevelDescriptor.HasValue)
        {
            var index = _visibleLevelDescriptors.FindIndex(descriptor => descriptor.Equals(_selectedLevelDescriptor.Value));
            if (index >= 0)
            {
                _uiManager.SelectFileByIndex(index);
                return;
            }
        }

        _uiManager.SelectFirstFile();
    }

    /// <summary>
    /// Обработка выбора нового спрайта в UI.
    /// </summary>
    private void HandleSpriteSelected(string spriteName)
    {
        if (spriteName == null)
        {
            TilemapEditorTool.SetActiveEditorTool(typeof(EraseTool));
            _uiManager.SetObstacleTypeForSelectedSprite(null);
        }
        else
        {
            SetActiveTileInBrush(spriteName);
            _uiManager.SetObstacleTypeForSelectedSprite(spriteName);
        }
    }

    /// <summary>
    /// Загружает PatternsCollection напрямую в Templates mode, без выбора файла из списка.
    /// </summary>
    private void LoadTemplatesDirectly()
    {
        _uiManager.ReleaseObstacleSprites();
        SpriteLoader.ReleaseSpritesAndClearCache();

        _currentLevelRef = null;
        _selectedFile = Path.Combine(_levelsDirectory, "PatternsCollection.json");

        _patternsCollection = LevelDataManager.LoadPatternsCollection();
        if (_patternsCollection == null || _patternsCollection.patterns.Count == 0)
        {
            Debug.LogWarning("Не удалось загрузить PatternsCollection");
            ClearScene();
            return;
        }

        _locationTheme = LevelDataManager.LoadLocationTheme("01_New_York");
        _currentLevelInfo = ResolveTemplatesForDisplay(_patternsCollection, _locationTheme);
        _patternSequencePanel.Hide();
        _spriteOverridePanel.Hide();

        float singlePatternWidth = _patternDurationMinutes * 60f * 3.8f;
        float totalWidth = Math.Max(singlePatternWidth, singlePatternWidth);

        var tilemapGameObject = SceneCreator.CreateSceneWithTilemap((int)totalWidth, "01_New_York", "morning");
        _tipeMapInScene = tilemapGameObject.GetComponent<Tilemap>();

        if (_currentLevelInfo.patterns.Count == 0)
        {
            _uiManager.UpdatePatternsList(new List<string>());
            _currentPattern = null;
            _selectedPatternIndex = -1;
            return;
        }

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();
        _uiManager.UpdatePatternsList(patternNames);
        _uiManager.SelectFirstPattern();
    }

    /// <summary>
    /// Обработка выбора файла уровня из списка.
    /// </summary>
    private void HandleFileSelected(LevelFileDescriptor selectedDescriptor)
    {
        _uiManager.ReleaseObstacleSprites();
        SpriteLoader.ReleaseSpritesAndClearCache();

        _selectedLevelDescriptor = selectedDescriptor;
        _selectedFile = selectedDescriptor.AbsolutePath;

        if (string.IsNullOrEmpty(_selectedFile))
        {
            Debug.LogWarning("Не выбран файл уровня.");
            return;
        }

        var isTemplateLocation = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);

        if (isTemplateLocation)
        {
            _currentLevelRef = null;
            _patternsCollection = LevelDataManager.LoadPatternsCollection();
            if (_patternsCollection == null || _patternsCollection.patterns.Count == 0)
            {
                Debug.LogWarning($"Не удалось загрузить PatternsCollection");
                return;
            }

            // For display, resolve using NY theme (as templates reuse NY sprites)
            _locationTheme = LevelDataManager.LoadLocationTheme("01_New_York");

            // Build a virtual LevelInfoRef from the PatternsCollection for display
            _currentLevelInfo = ResolveTemplatesForDisplay(_patternsCollection, _locationTheme);
            _patternSequencePanel.Hide();
            _spriteOverridePanel.Hide();
        }
        else
        {
            _currentLevelRef = LevelDataManager.LoadLevelRef(_selectedFile);
            if (_currentLevelRef == null)
            {
                Debug.LogWarning($"Не удалось загрузить уровень (ref) из файла {_selectedFile}");
                return;
            }

            _patternsCollection = LevelDataManager.LoadPatternsCollection();
            _locationTheme = LevelDataManager.LoadLocationTheme(_currentLocationName);
            _currentLevelInfo = LevelResolver.Resolve(_currentLevelRef, _patternsCollection, _locationTheme);
            _patternSequencePanel.Show(_currentLevelRef, _patternsCollection);
            _spriteOverridePanel.Hide();
        }

        // Вычисляем общую ширину уровня.
        // Для локаций: все паттерны × длительность одного.
        // Для шаблонов: одна длительность (редактируются по одному).
        float singlePatternWidth = _patternDurationMinutes * 60f * 3.8f;
        int patternCount = isTemplateLocation ? 1 : _currentLevelInfo.patterns.Count;
        float totalWidth = Math.Max(singlePatternWidth, patternCount * singlePatternWidth);

        // Создаём сцену с фоном и дорогой по naming convention
        string locationForBg = isTemplateLocation ? "01_New_York" : _currentLocationName;
        string daypartSlug = isTemplateLocation ? "morning" : _selectedDaypart.ToString().ToLowerInvariant();
        var tilemapGameObject = SceneCreator.CreateSceneWithTilemap((int)totalWidth, locationForBg, daypartSlug);
        _tipeMapInScene = tilemapGameObject.GetComponent<Tilemap>();

        // Load patterns (obstacles) for both Templates and Locations
        if (_currentLevelInfo.patterns.Count == 0)
        {
            Debug.LogWarning("Уровень не содержит паттернов.");
            _uiManager.UpdatePatternsList(new List<string>());
            _currentPattern = null;
            _selectedPatternIndex = -1;
            
            // For locations without patterns, still load decorations
            if (!isTemplateLocation)
            {
                LoadDecorationsToTilemap();
            }
            
            return;
        }

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();
        _uiManager.UpdatePatternsList(patternNames);
        _uiManager.SelectFirstPattern();

        // For specific locations: also load decorations on top of obstacles
        if (!isTemplateLocation)
        {
            LoadDecorationsToTilemap();
        }
    }



    /// <summary>
    /// Обработка изменения выбранного паттерна.
    /// </summary>
    private void HandlePatternSelected(int selectedIndex)
    {
        // Сохраняем описание ПРЕЖДЕ переключения
        if (_selectedPatternIndex >= 0 && _selectedPatternIndex < _currentLevelInfo.patterns.Count)
        {
            HandlePatternDescriptionChanged(_uiManager.CurrentPatternDescription);
        }

        _selectedPatternIndex = selectedIndex;
        if (_selectedPatternIndex < 0 || _selectedPatternIndex >= _currentLevelInfo.patterns.Count)
        {
            _currentPattern = null;
            Debug.LogWarning("Выбранный паттерн некорректен.");
            return;
        }

        _currentPattern = _currentLevelInfo.patterns[_selectedPatternIndex];

        _uiManager.UpdatePatternNameField(_currentPattern.name);
        _uiManager.UpdatePatternDescriptionField(_currentPattern.desсription);

        AddTilesToTilemap();
    }

    /// <summary>
    /// Изменение флажка, указывающего, что коллектабл размещается на крыше.
    /// </summary>
    private void HandleIsCollectableOnRoofToggleChanged(bool newValue)
    {
        _isCollectableOnRoof = newValue;
        Debug.Log($"[LevelTilemapEditor] IsCollectableOnRoof state changed: {_isCollectableOnRoof}");
    }

    /// <summary>
    /// Очищает тайлмап и расставляет тайлы из текущего паттерна.
    /// </summary>
    private void AddTilesToTilemap()
    {
        if (_currentPattern == null || _tipeMapInScene == null)
            return;

        _isTilemapBulkOperation = true;
        _tipeMapInScene.ClearAllTiles();

        var positions = new List<Vector3Int>();
        var tiles = new List<TileBase>();

        foreach (var obstacle in _currentPattern.obstacles)
        {
            var loadedSprite = SpriteLoader.LoadSpriteSync(obstacle.spriteName);
            if (loadedSprite != null)
            {
                var tile = CreateInstance<Tile>();
                tile.sprite = loadedSprite;
                tile.name = obstacle.spriteName;

                var worldPos = new Vector3(obstacle.x, obstacle.y, 0f);
                var cellPos = _tipeMapInScene.WorldToCell(worldPos);

                positions.Add(cellPos);
                tiles.Add(tile);
            }
            else
            {
                Debug.LogWarning($"Не удалось загрузить спрайт: {obstacle.spriteName}");
            }
        }

        _tipeMapInScene.SetTiles(positions.ToArray(), tiles.ToArray());

        // Restore decorations after clearing tilemap (they are level-wide, not per-pattern)
        var isTemplate = string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);
        if (!isTemplate)
        {
            LoadDecorationsToTilemap();
        }

        EditorUtility.SetDirty(_tipeMapInScene.gameObject);
        _isTilemapBulkOperation = false;
    }

    /// <summary>
    /// Инициализация кнопок управления паттернами.
    /// </summary>
    private void InitializePatternButtons()
    {
        var addPatternButton = rootVisualElement.Q<Button>("add-pattern-btn");
        var removePatternButton = rootVisualElement.Q<Button>("remove-pattern-btn");
        var duplicatePatternButton = rootVisualElement.Q<Button>("duplicate-pattern-btn");
        var moveUpButton = rootVisualElement.Q<Button>("move-up-btn");
        var moveDownButton = rootVisualElement.Q<Button>("move-down-btn");

        moveUpButton.clicked += MovePatternUp;
        moveDownButton.clicked += MovePatternDown;
        addPatternButton.clicked += AddNewPattern;
        removePatternButton.clicked += RemovePattern;
        duplicatePatternButton.clicked += DuplicatePattern;
    }

    /// <summary>
    /// Инициализация директории для шаблонов дизайна уровней.
    /// </summary>
    private void InitializeLevelDesignTemplateDirectory()
    {
        _levelDesignTemplatesDirectory =
            Path.Combine(Consts.LocationsPath, Consts.TemplatesLocationName);

        Directory.CreateDirectory(_levelDesignTemplatesDirectory); // создаст, если нет
        AssetDatabase.Refresh();                                   // чтобы папка появилась в Project
    }


    /// <summary>
    /// Перемещает паттерн на одну позицию вверх.
    /// </summary>
    private void MovePatternUp()
    {
        if (_selectedPatternIndex <= 0) return;

        var selectedIndex = _selectedPatternIndex;
        var pattern = _currentLevelInfo.patterns[selectedIndex];

        _currentLevelInfo.patterns.RemoveAt(selectedIndex);
        _currentLevelInfo.patterns.Insert(selectedIndex - 1, pattern);

        _selectedPatternIndex = selectedIndex - 1;

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        _uiManager.UpdatePatternsList(patternNames, _selectedPatternIndex);
        Debug.Log($"Паттерн перемещен вверх: {pattern.name}");
    }

    /// <summary>
    /// Перемещает паттерн на одну позицию вниз.
    /// </summary>
    private void MovePatternDown()
    {
        if (_selectedPatternIndex < 0 || _selectedPatternIndex >= _currentLevelInfo.patterns.Count - 1) return;

        var selectedIndex = _selectedPatternIndex;
        var pattern = _currentLevelInfo.patterns[selectedIndex];

        _currentLevelInfo.patterns.RemoveAt(selectedIndex);
        _currentLevelInfo.patterns.Insert(selectedIndex + 1, pattern);
        _selectedPatternIndex = selectedIndex + 1;

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        _uiManager.UpdatePatternsList(patternNames, _selectedPatternIndex);

        Debug.Log($"Паттерн перемещен вниз: {pattern.name}");
    }

    /// <summary>
    /// Добавляет новый паттерн в список.
    /// </summary>
    private void AddNewPattern()
    {
        var newPattern = new Pattern
        {
            name = $"Pattern {_currentLevelInfo.patterns.Count + 1}",
            desсription = string.Empty,
            obstacles = new List<ObstacleModel>()
        };

        _currentLevelInfo.patterns.Add(newPattern);

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        _uiManager.UpdatePatternsList(patternNames);
        _selectedPatternIndex = _currentLevelInfo.patterns.Count - 1;

        Debug.Log($"Добавлен новый паттерн: {newPattern.name}");
    }

    /// <summary>
    /// Создает полный дубликат выбранного паттерна.
    /// </summary>
    private void DuplicatePattern()
    {
        if (_currentLevelInfo == null)
        {
            Debug.LogWarning("Невозможно дублировать паттерн: информация об уровне отсутствует.");
            return;
        }

        if (_selectedPatternIndex < 0 || _selectedPatternIndex >= _currentLevelInfo.patterns.Count)
        {
            Debug.LogWarning("Невозможно дублировать паттерн: некорректный индекс выбранного паттерна.");
            return;
        }

        var originalPattern = _currentLevelInfo.patterns[_selectedPatternIndex];

        var duplicatedPattern = new Pattern
        {
            name = $"{originalPattern.name}_Duplicate",
            desсription = originalPattern.desсription,
            obstacles = originalPattern.obstacles?
                .Select(o => new ObstacleModel
                {
                    spriteName = o.spriteName,
                    type = o.type,
                    x = o.x,
                    y = o.y
                })
                .ToList() ?? new List<ObstacleModel>()
        };

        var insertIndex = _selectedPatternIndex + 1;
        _currentLevelInfo.patterns.Insert(insertIndex, duplicatedPattern);

        _selectedPatternIndex = insertIndex;
        _currentPattern = duplicatedPattern;

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();
        _uiManager.UpdatePatternsList(patternNames, _selectedPatternIndex);
        _uiManager.UpdatePatternNameField(duplicatedPattern.name);
        _uiManager.UpdatePatternDescriptionField(duplicatedPattern.desсription);

        AddTilesToTilemap();

        Debug.Log($"Создан дубликат паттерна: {duplicatedPattern.name}");
    }

    /// <summary>
    /// Удаляет выбранный паттерн из списка.
    /// </summary>
    private void RemovePattern()
    {
        if (_selectedPatternIndex < 0) return;

        var selectedIndex = _selectedPatternIndex;
        var patternToRemove = _currentLevelInfo.patterns[selectedIndex];

        _currentLevelInfo.patterns.RemoveAt(selectedIndex);

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        _uiManager.UpdatePatternsList(patternNames);
        _selectedPatternIndex = Math.Min(selectedIndex, _currentLevelInfo.patterns.Count - 1);

        Debug.Log($"Удален паттерн: {patternToRemove.name}");
    }

    /// <summary>
    /// Обновляет список спрайтов в текущей локации.
    /// </summary>
    private void UpdateSpritesInfoInCurrentLocation(string newValue)
    {
        _spritesDirectory = Path.Combine(Consts.LocationsPath, newValue, "sprites");
        var sprites = Directory.GetFiles(_spritesDirectory, $"*.{_spritesExt}", SearchOption.AllDirectories);

        _spritesNames = sprites.Select(Path.GetFileNameWithoutExtension).ToList();
    }

    /// <summary>
    /// Устанавливает выбранный тайл в кисть для рисования.
    /// </summary>
    private void SetActiveTileInBrush(string tileName)
    {
        var shortName = Path.GetFileNameWithoutExtension(tileName);
        var sprite = SpriteLoader.LoadSpriteSync(shortName);
        if (sprite == null)
        {
            Debug.LogError($"Sprite '{shortName}' not found for tile.");
            return;
        }

        var tile = CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.name = shortName;

        var brush = CreateInstance<GridBrush>();
        brush.name = "TemporaryBrush";
        brush.size = new Vector3Int(1, 1, 1);
        brush.pivot = new Vector3Int(0, 0, 0);
        brush.SetTile(Vector3Int.zero, tile);

        GridPaintingState.gridBrush = brush;
        TilemapEditorTool.SetActiveEditorTool(typeof(PaintTool));

        // открываем Tile Palette окно
        EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
    }

    /// <summary>
    /// Очищает сцену и сбрасывает состояние редактора при отсутствии уровней.
    /// </summary>
    private void ClearScene()
    {
        SceneCreator.CleanupOldSceneObjects(SceneManager.GetActiveScene());

        _tipeMapInScene = null;
        _selectedFile = null;
        _currentLevelInfo = null;
        _currentLevelRef = null;
        _currentPattern = null;
        _selectedPatternIndex = -1;
        _selectedLevelDescriptor = null;

        _uiManager.UpdatePatternsList(new List<string>());
        _patternSequencePanel.Hide();
        _spriteOverridePanel.Hide();
    }

    /// <summary>
    /// Сброс редактора до исходного состояния.
    /// </summary>
    private void HandleResetClicked()
    {
        if (_tipeMapInScene != null)
        {
            DestroyImmediate(_tipeMapInScene.gameObject);
            _tipeMapInScene = null;
        }

        _currentLevelInfo = null;
        _selectedFile = null;
        _selectedPatternIndex = -1;
        _currentPattern = null;
        _isCollectableOnRoof = false;
        _selectedLevelDescriptor = null;
        _allLevelDescriptors.Clear();
        _visibleLevelDescriptors.Clear();
        _selectedDaypart = PartOfDayEnum.Morning;

    _uiManager.ReleaseObstacleSprites();
        SpriteLoader.ReleaseSpritesAndClearCache();

        rootVisualElement.Clear();
        CreateGUI();
    }

    private void HandlePatternDurationChanged(float newDuration)
    {
        _patternDurationMinutes = newDuration;
        Debug.Log($"Pattern duration changed to {_patternDurationMinutes} minutes");
    }

    private void HandlePatternNameChanged(string newName)
    {
        if (_currentLocationName != Consts.TemplatesLocationName)
        {
            Debug.Log("Cannot update pattern name: only available for level_design_templates location");
            return;
        }

        if (_selectedPatternIndex < 0 || _selectedPatternIndex >= _currentLevelInfo.patterns.Count)
        {
            Debug.LogWarning("Cannot update pattern name: No pattern selected or invalid index.");
            return;
        }

        Debug.Log($"[Editor] Renaming pattern from '{_currentLevelInfo.patterns[_selectedPatternIndex].name}' to '{newName}'");

        // Save the current selection index
        int indexToSelect = _selectedPatternIndex;

        // Update the pattern name in the data model
        var pattern = _currentLevelInfo.patterns[_selectedPatternIndex];
        pattern.name = newName;
        _currentLevelInfo.patterns[_selectedPatternIndex] = pattern;

        // Save changes to JSON file
        if (!string.IsNullOrEmpty(_selectedFile))
        {
            Debug.Log($"[Editor] Saving changes to file: {_selectedFile}");
            LevelDataManager.SaveLevel(_currentLevelInfo, _selectedFile);
        }
        else
        {
            Debug.LogWarning("Cannot save pattern name change: No file selected.");
        }

        // Get updated pattern names for UI
        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        // Update UI with pattern names and preserve selection
        Debug.Log($"[Editor] Updating patterns list, preserving selection at index {indexToSelect}");
        _uiManager.UpdatePatternsList(patternNames, indexToSelect);

        // Make sure the pattern name field still shows the updated name
        _uiManager.UpdatePatternNameField(newName);

        Debug.Log($"[Editor] Pattern rename complete: {newName}");
    }

    private void HandlePatternDescriptionChanged(string newDesc)
    {
        if (_currentLocationName != Consts.TemplatesLocationName)
        {
            Debug.Log("Cannot update pattern description: only available for level_design_templates location");
            return;
        }

        if (_selectedPatternIndex < 0 ||
            _selectedPatternIndex >= _currentLevelInfo.patterns.Count)
        {
            Debug.LogWarning("Cannot update pattern description: invalid index.");
            return;
        }

        // Обновляем данные модели
        var pattern = _currentLevelInfo.patterns[_selectedPatternIndex];
        pattern.desсription = newDesc ?? "";
        _currentLevelInfo.patterns[_selectedPatternIndex] = pattern;
    }


    /// <summary>
    /// Подписка на события UI и Tilemap.
    /// </summary>
    private void SubscribeEvents()
    {
        _uiManager.OnCreateLevelClicked += HandleCreateLevelClicked;
        _uiManager.OnSaveLevelClicked += HandleSaveLevelClicked;
        _uiManager.OnLocationChanged += HandleLocationChanged;
        _uiManager.OnSpriteSelected += HandleSpriteSelected;
        _uiManager.OnFileSelected += HandleFileSelected;
        _uiManager.OnPatternSelected += HandlePatternSelected;
        _uiManager.OnIsCollectableOnRoofToggleChanged += HandleIsCollectableOnRoofToggleChanged;
        _uiManager.OnResetClicked += HandleResetClicked;
        _uiManager.OnPatternDurationChanged += HandlePatternDurationChanged;
        _uiManager.OnPatternNameChanged += HandlePatternNameChanged;
        _uiManager.OnPatternDescriptionChanged += HandlePatternDescriptionChanged;
        _uiManager.OnDaypartChanged += HandleDaypartChanged;

        _patternSequencePanel.OnSequenceChanged += HandlePatternSequenceChanged;
        _spriteOverridePanel.OnOverrideChanged += HandleSpriteOverrideChanged;

        Tilemap.tilemapTileChanged += OnTileChanged;
    }

    /// <summary>
    /// Отписка от событий UI и Tilemap.
    /// </summary>
    private void UnsubscribeEvents()
    {
        if (_uiManager != null)
        {
            _uiManager.OnCreateLevelClicked -= HandleCreateLevelClicked;
            _uiManager.OnSaveLevelClicked -= HandleSaveLevelClicked;
            _uiManager.OnLocationChanged -= HandleLocationChanged;
            _uiManager.OnSpriteSelected -= HandleSpriteSelected;
            _uiManager.OnFileSelected -= HandleFileSelected;
            _uiManager.OnPatternSelected -= HandlePatternSelected;
            _uiManager.OnIsCollectableOnRoofToggleChanged -= HandleIsCollectableOnRoofToggleChanged;
            _uiManager.OnResetClicked -= HandleResetClicked;
            _uiManager.OnPatternDurationChanged -= HandlePatternDurationChanged;
            _uiManager.OnPatternNameChanged -= HandlePatternNameChanged;
            _uiManager.OnPatternDescriptionChanged -= HandlePatternDescriptionChanged;
            _uiManager.OnDaypartChanged -= HandleDaypartChanged;
        }

        if (_patternSequencePanel != null)
            _patternSequencePanel.OnSequenceChanged -= HandlePatternSequenceChanged;
        if (_spriteOverridePanel != null)
            _spriteOverridePanel.OnOverrideChanged -= HandleSpriteOverrideChanged;

        Tilemap.tilemapTileChanged -= OnTileChanged;
    }

    /// <summary>
    /// Re-resolves and refreshes tilemap when pattern sequence changes.
    /// </summary>
    private void HandlePatternSequenceChanged()
    {
        if (_currentLevelRef == null || _patternsCollection == null || _locationTheme == null)
            return;

        _currentLevelInfo = LevelResolver.Resolve(_currentLevelRef, _patternsCollection, _locationTheme);

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();
        _uiManager.UpdatePatternsList(patternNames);

        if (patternNames.Count > 0)
            _uiManager.SelectFirstPattern();
    }

    /// <summary>
    /// Re-resolves and refreshes current pattern when a sprite override changes.
    /// </summary>
    private void HandleSpriteOverrideChanged()
    {
        if (_currentLevelRef == null || _patternsCollection == null || _locationTheme == null)
            return;

        _currentLevelInfo = LevelResolver.Resolve(_currentLevelRef, _patternsCollection, _locationTheme);
        AddTilesToTilemap();
    }

    /// <summary>
    /// Converts PatternsCollection to LevelInfo for tilemap display in Templates mode.
    /// Uses the given theme to resolve sprite names for display purposes.
    /// </summary>
    private static LevelInfo ResolveTemplatesForDisplay(PatternsCollection pc, LocationTheme theme)
    {
        var levelRef = new LevelInfoRef
        {
            location = "01_New_York"
        };

        foreach (var template in pc.patterns)
        {
            levelRef.patternSequence.Add(new PatternRef
            {
                @ref = template.name,
                spriteSeed = 0,
                overrides = new List<SpriteOverride>()
            });
        }

        return LevelResolver.Resolve(levelRef, pc, theme);
    }

    /// <summary>
    /// Syncs current tilemap state back into PatternsCollection.
    /// Updates obstacles in the currently selected pattern template.
    /// </summary>
    private void SyncTemplatesFromLevelInfo()
    {
        if (_patternsCollection == null || _currentLevelInfo == null)
            return;

        // Only sync the currently selected pattern
        if (_selectedPatternIndex < 0 || _selectedPatternIndex >= _patternsCollection.patterns.Count)
            return;

        var template = _patternsCollection.patterns[_selectedPatternIndex];
        var resolvedPattern = _currentLevelInfo.patterns[_selectedPatternIndex];

        // Rebuild obstacles from resolved pattern — keep existing ids where possible
        var newObstacles = new List<ObstacleSlot>();
        int nextId = template.nextObstacleId;

        foreach (var obstacle in resolvedPattern.obstacles)
        {
            // Try to find existing slot by position
            var existingSlot = template.obstacles.Find(s =>
                s.type == obstacle.type &&
                Math.Abs(s.x - obstacle.x) < 0.01f &&
                Math.Abs(s.y - obstacle.y) < 0.01f);

            newObstacles.Add(new ObstacleSlot
            {
                id = existingSlot?.id ?? nextId++,
                type = obstacle.type,
                x = obstacle.x,
                y = obstacle.y
            });
        }

        template.obstacles = newObstacles;
        template.nextObstacleId = nextId;
        template.name = resolvedPattern.name;
        template.description = resolvedPattern.desсription ?? "";
    }
}
