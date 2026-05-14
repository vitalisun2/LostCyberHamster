using Assets.Editor.LevelEditor;
using Assets.Scripts.Common;
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
using System.Text.RegularExpressions;

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

    private LevelTilemapUi _uiManager;
    private LevelInfo _currentLevelInfo;
    private LevelInfoRef _currentLevelRef;
    private PatternsCollection _patternsCollection;
    private LocationTheme _locationTheme;
    private string _selectedFile;
    private int _selectedPatternIndex = -1;
    private Tilemap _tilemapInScene;
    private bool _isObjectOnRoof;

    private bool IsTemplateMode => string.Equals(_currentLocationName,
        Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase);

    private Pattern CurrentPattern =>
        _selectedPatternIndex >= 0 && _currentLevelInfo?.patterns != null
            && _selectedPatternIndex < _currentLevelInfo.patterns.Count
            ? _currentLevelInfo.patterns[_selectedPatternIndex]
            : null;
    private bool _isTilemapBulkOperation;
    private PartOfDayEnum _selectedDaypart = PartOfDayEnum.Morning;
    private List<LevelFileDescriptor> _allLevelDescriptors = new();
    private List<LevelFileDescriptor> _visibleLevelDescriptors = new();
    private LevelFileDescriptor? _selectedLevelDescriptor;

    private PatternSequencePanel _patternSequencePanel;
    private SpriteOverridePanel _spriteOverridePanel;

    private List<string> _allPatternNames = new();
    private List<int> _filteredPatternIndices = new();
    private string _patternSearchFilter = "";

    /// <summary>
    /// Маппинг cell position → (patternIndex, obstacleIndex) для определения паттерна по клику на тайл.
    /// </summary>
    private Dictionary<Vector3Int, (int patternIndex, int obstacleIndex)> _cellToPatternMap = new();

    /// <summary>
    /// Реальные world bounds каждого паттерна при последовательной отрисовке.
    /// </summary>
    private List<Bounds> _patternBounds = new();

    private const float PatternGap = 2f;
    private const float PatternFrameHorizontalPadding = 1.5f;
    private const float PatternFrameVerticalPadding = 0.75f;
    private const float MinPatternFrameWidth = 6f;
    private const float MinPatternFrameHeight = 4f;
    private const float DefaultPatternFrameCenterY = -2.3f;
    private const float PatternBoundaryInset = 0.15f;
    private const float PatternBoundaryLineThickness = 4f;
    private const float LowerRoadTileZOffset = -0.1f;
    private static readonly Color PatternBoundaryColor = new Color(0.12f, 0.95f, 0.18f, 1f);
    private static readonly Color SelectedPatternBoundaryColor = new Color(0.25f, 1f, 0.25f, 1f);
    private static readonly Regex PatternNameSuffixRegex = new Regex(@"^(.*?)(\d+)$", RegexOptions.Compiled);

    private readonly List<PatternOverlaySlot> _patternOverlaySlots = new();

    private struct PatternOverlaySlot
    {
        public int PatternIndex;
        public float LeftX;
        public float RightX;
        public string Name;
        public bool IsRelief;
    }

    private string _levelsDirectory;
    private string _levelDesignTemplatesDirectory;
    private string _spritesDirectory;
    private string _currentLocationName;
    private List<string> _spritesNames { get; set; }
    
    // Scene management for non-intrusive workflow
    private string _originalScenePath;
    private List<GameObject> _hiddenRootObjects = new();
    private bool _isActive;

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
        _patternSequencePanel = new PatternSequencePanel();
        _spriteOverridePanel = new SpriteOverridePanel(rootVisualElement);

        // Insert PatternSequencePanel right after the files list section
        var filesSection = rootVisualElement.Q<ListView>("files-list-view")?.parent;
        if (filesSection != null)
        {
            var container = filesSection.parent;
            int index = container.IndexOf(filesSection);
            container.Insert(index + 1, _patternSequencePanel.Root);
        }
        SubscribeEvents();
        InitializePatternButtons();
        InitializeLevelDesignTemplateDirectory();
    }

    private void OnEnable()
    {
        ActivateSceneIsolation();
    }

    /// <summary>
    /// Прячет объекты текущей сцены и переводит редактор в активный режим.
    /// </summary>
    private void ActivateSceneIsolation()
    {
        if (_isActive) return;

        var currentScene = SceneManager.GetActiveScene();
        _originalScenePath = currentScene.path;

        _hiddenRootObjects.Clear();
        foreach (var rootObject in currentScene.GetRootGameObjects())
        {
            if (rootObject.activeSelf)
            {
                rootObject.SetActive(false);
                _hiddenRootObjects.Add(rootObject);
            }
        }

        SceneView.duringSceneGui += OnSceneGUI;
        _isActive = true;
    }

    private void OnDisable()
    {
        Deactivate();
        UnsubscribeEvents();
    }

    /// <summary>
    /// Деактивирует редактор: очищает тайлмап, освобождает ресурсы, восстанавливает исходную сцену.
    /// Окно при этом не закрывается.
    /// </summary>
    private void Deactivate()
    {
        if (!_isActive) return;

        SceneView.duringSceneGui -= OnSceneGUI;
        _uiManager?.ReleaseObstacleSprites();
        SpriteLoader.ReleaseSpritesAndClearCache();

        _tilemapInScene = null;
        _currentLevelInfo = null;
        _currentLevelRef = null;
        _patternsCollection = null;
        _locationTheme = null;
        _selectedFile = null;
        _selectedPatternIndex = -1;
        _isObjectOnRoof = false;
        _selectedLevelDescriptor = null;
        _allLevelDescriptors.Clear();
        _visibleLevelDescriptors.Clear();
        _selectedDaypart = PartOfDayEnum.Morning;
        _currentLocationName = null;
        _levelsDirectory = null;
        _cellToPatternMap.Clear();
        _patternBounds.Clear();
        _patternOverlaySlots.Clear();
        _allPatternNames.Clear();
        _filteredPatternIndices.Clear();

        _isActive = false;

        // Не восстанавливаем сцену во время компиляции/обновления и переходов playmode,
        // чтобы не конфликтовать с внутренними перечислителями Hierarchy/Search.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            _hiddenRootObjects.Clear();
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
            foreach (var obj in _hiddenRootObjects)
            {
                if (obj != null)
                {
                    try
                    {
                        obj.SetActive(true);
                    }
                    catch (MissingReferenceException)
                    {
                        // Object мог быть уничтожен Unity при перезагрузке/очистке сцены.
                    }
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

        if (changedTilemap != _tilemapInScene)
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
        // For decoration sprites (starting with "decor"), allow placement only in non-template locations
        if (tile.sprite.name.StartsWith("decor", StringComparison.OrdinalIgnoreCase))
        {
            if (IsTemplateMode)
            {
                // Decorations not allowed in Templates
                changedTilemap.SetTile(cellPosition, null);
                Debug.LogWarning("Decorations cannot be placed in Templates. Use specific locations (New York, Paris, etc.)");
                return;
            }
            return; // Decorations can be placed freely in locations
        }

        // For obstacle sprites in specific locations (not templates), block editing
        if (!IsTemplateMode)
        {
            // Obstacles are read-only in specific locations
            changedTilemap.SetTile(cellPosition, null);
            Debug.LogWarning("Obstacles are read-only in specific locations. Edit them in Templates mode.");
            return;
        }

        // For obstacle sprites in Templates, apply placement rules
        if (!ObstacleSpriteTypeMappingsManager.TryGetType(tile.sprite.name, out var obstacleType))
        {
            changedTilemap.SetTile(cellPosition, null);
            Debug.LogWarning($"No mapping found for sprite '{tile.sprite.name}'. Tile removed.");
            return;
        }

        var strategy = TilePlacementStrategies.GetStrategyForType(obstacleType, _isObjectOnRoof);
        var initialWorldPos = changedTilemap.CellToWorld(cellPosition);

        if (!strategy.TryPlaceTile(changedTilemap, tile, initialWorldPos, out var finalWorldPos))
        {
            changedTilemap.SetTile(cellPosition, null);
            Debug.LogWarning($"Tile '{tile.sprite.name}' could not be placed according to the rules and was removed.");
            return;
        }

        var finalCellPos = changedTilemap.WorldToCell(finalWorldPos);
        if (finalCellPos != cellPosition)
        {
            changedTilemap.SetTile(cellPosition, null);
            changedTilemap.SetTile(finalCellPos, tile);
        }

        ApplyExactTileWorldPosition(changedTilemap, finalCellPos, finalWorldPos);
    }

    /// <summary>
    /// Обновляет информацию об уровне, основываясь на содержимом тайлмапа.
    /// </summary>
    private void UpdateCurrentLevelInfoFromTilemap()
    {
        if (_tilemapInScene == null || _currentLevelInfo == null)
        {
            Debug.LogWarning("Tilemap или CurrentLevelInfo не инициализированы.");
            return;
        }

        // For specific locations (not templates), decorations are synced only on save
        if (!IsTemplateMode)
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

        foreach (var cellPos in _tilemapInScene.cellBounds.allPositionsWithin)
        {
            var tile = _tilemapInScene.GetTile(cellPos) as Tile;
            if (tile == null || tile.sprite == null)
                continue;

            updatedObstacles.Add(CreateObstacleModelFromCell(_tilemapInScene, cellPos, tile));
        }

        selectedPattern.obstacles = updatedObstacles;
        _currentLevelInfo.patterns[_selectedPatternIndex] = selectedPattern;
    }

    /// <summary>
    /// Создаёт модель препятствия из клетки тайлмапа.
    /// </summary>
    private ObstacleModel CreateObstacleModelFromCell(Tilemap tilemap, Vector3Int cellPos, Tile tile)
    {
        var worldPos = GetExactTileWorldPosition(tilemap, cellPos);
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
        if (_tilemapInScene == null || _currentLevelInfo == null)
        {
            Debug.LogWarning("[LevelTilemapEditor] Tilemap или CurrentLevelInfo не инициализированы.");
            return;
        }

        var decorationTiles = new List<DecorationTile>();

        foreach (var cellPos in _tilemapInScene.cellBounds.allPositionsWithin)
        {
            var tile = _tilemapInScene.GetTile(cellPos) as Tile;
            if (tile == null || tile.sprite == null)
                continue;

            // Only save decoration sprites (starting with "decor")
            if (!tile.name.StartsWith("decor", StringComparison.OrdinalIgnoreCase))
                continue;

            // Extract rotation and scale from the tilemap transform matrix
            var matrix = _tilemapInScene.GetTransformMatrix(cellPos);
            var scale = matrix.lossyScale;
            var rotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
            float zRotation = rotation.eulerAngles.z;

            decorationTiles.Add(new DecorationTile
            {
                name = tile.name,
                xPos = cellPos.x,
                yPos = cellPos.y,
                rotation = zRotation,
                scaleX = scale.x,
                scaleY = scale.y
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
        if (_tilemapInScene == null || _currentLevelInfo == null)
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
            _tilemapInScene.SetTile(cellPos, tile);

            // Restore rotation and scale via transform matrix
            if (decorTile.rotation != 0f || decorTile.scaleX != 1f || decorTile.scaleY != 1f)
            {
                var matrix = Matrix4x4.TRS(
                    Vector3.zero,
                    Quaternion.Euler(0f, 0f, decorTile.rotation),
                    new Vector3(decorTile.scaleX, decorTile.scaleY, 1f));
                _tilemapInScene.SetTransformMatrix(cellPos, matrix);
            }

            loadedCount++;
        }

        DebugManager.DiagLog($"[LevelTilemapEditor] Loaded {loadedCount} decoration tiles to Tilemap.");
    }

    /// <summary>
    /// Сохраняет текущий уровень.
    /// </summary>
    private void HandleSaveLevelClicked()
    {
        if (IsTemplateMode)
        {
            Debug.Log($"Сохранение PatternsCollection: {_selectedFile}");
            SavePatternsCollectionToDisk();
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
        if (string.IsNullOrWhiteSpace(_currentLocationName) || string.IsNullOrWhiteSpace(_levelsDirectory))
        {
            EditorUtility.DisplayDialog("Location Required", "Выбери локацию перед созданием уровня.", "OK");
            return;
        }

        if (IsTemplateMode)
        {
            CreateLevelPromptWindow.Show(
                "Create Template",
                "Template Name",
                "new_template",
                "OK",
                CreateTemplateWithName);
        }
        else
        {
            CreateLevelPromptWindow.Show(
                "Create Level",
                "Level Name",
                LevelDataManager.GetNextAvailableLevelKey(_levelsDirectory),
                "OK",
                CreateLevelWithName);
        }
    }

    private LevelInfo CreateDefaultLevelInfo()
    {
        return new LevelInfo
        {
            skyTexture = string.Empty,
            roadTexture = string.Empty,
            decorationPatterns = new List<DecorationPattern>(),
            patterns = new List<Pattern>()
        };
    }

    private LevelInfoRef CreateDefaultLevelInfoRef()
    {
        return new LevelInfoRef
        {
            skyTexture = string.Empty,
            roadTexture = string.Empty,
            location = _currentLocationName,
            patternSequence = new List<PatternRef>(),
            decorationPatterns = new List<DecorationPattern>()
        };
    }

    /// <summary>
    /// Реакция на изменение локации в UI.
    /// </summary>
    private void HandleLocationChanged(string newValue)
    {
        ActivateSceneIsolation();

        _currentLocationName = newValue;

    _uiManager.SetObstaclesSpritesListView(newValue, _spritesExt);
        _uiManager.AddCollectablesToSpritesListView();

        /* Загружаем маппинг спрайт‑типов для выбранной локации.
           Templates не имеет своих спрайтов — используем fallback-локацию. */
        var mappingLocation = IsTemplateMode
            ? Consts.TemplatesFallbackLocation
            : newValue;
        ObstacleSpriteTypeMappingsManager.LoadBindings(mappingLocation, success =>
        {
            if (!success)
                Debug.LogWarning($"No mapping file yet for '{newValue}'. It will be created on first save.");
        });

        _uiManager.ApplyModeUI(IsTemplateMode);

        if (!IsTemplateMode)
        {
            _selectedDaypart = PartOfDayEnum.Morning;
            _uiManager.SetSelectedDaypart(_selectedDaypart);
        }

        _selectedLevelDescriptor = null;

        _levelsDirectory = Path.Combine(Consts.LocationsPath, newValue, "levels");
        UpdateSpritesInfoInCurrentLocation(newValue);

        if (IsTemplateMode)
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

        _visibleLevelDescriptors = IsTemplateMode
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

    private bool TrySelectLevelByPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        var normalizedTargetPath = Path.GetFullPath(absolutePath);
        var index = _visibleLevelDescriptors.FindIndex(descriptor =>
            string.Equals(Path.GetFullPath(descriptor.AbsolutePath), normalizedTargetPath, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return false;
        }

        _uiManager.SelectFileByIndex(index);
        return true;
    }

    private void CreateTemplateWithName(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return;

        var newLevelInfo = CreateDefaultLevelInfo();
        var createdLevelPath = LevelDataManager.CreateNewTemplate(newLevelInfo, templateName, _levelsDirectory, _spritesNames);
        FinalizeCreatedLevel(createdLevelPath);
    }

    private void CreateLevelWithName(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return;

        var newLevelRef = CreateDefaultLevelInfoRef();
        var createdLevelPath = LevelDataManager.CreateNewLevelRef(newLevelRef, _levelsDirectory, _selectedDaypart, levelName, _spritesNames);
        FinalizeCreatedLevel(createdLevelPath);
    }

    private void FinalizeCreatedLevel(string createdLevelPath)
    {
        if (string.IsNullOrWhiteSpace(createdLevelPath))
            return;

        AssetDatabase.Refresh();
        RefreshLevelFilesList(reloadFromDisk: true, autoSelectFirst: false);
        TrySelectLevelByPath(createdLevelPath);
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

        _locationTheme = LevelDataManager.LoadLocationTheme(Consts.TemplatesFallbackLocation);
        _currentLevelInfo = ResolveTemplatesForDisplay(_patternsCollection, _locationTheme);
        _patternSequencePanel.Hide();
        _spriteOverridePanel.Hide();

        int sceneWidth = Math.Max(DefaultTilemapWidth, (int)Math.Ceiling(ComputeMaxPatternWidth(_currentLevelInfo.patterns)));
        var tilemapGameObject = SceneCreator.CreateSceneWithTilemap(sceneWidth, Consts.TemplatesFallbackLocation, "morning");
        _tilemapInScene = tilemapGameObject.GetComponent<Tilemap>();

        if (_currentLevelInfo.patterns.Count == 0)
        {
            _uiManager.UpdatePatternsList(new List<string>());
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

        if (IsTemplateMode)
        {
            _currentLevelRef = null;
            _patternsCollection = LevelDataManager.LoadPatternsCollection();
            if (_patternsCollection == null || _patternsCollection.patterns.Count == 0)
            {
                Debug.LogWarning($"Не удалось загрузить PatternsCollection");
                return;
            }

            _locationTheme = LevelDataManager.LoadLocationTheme(Consts.TemplatesFallbackLocation);

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
        float totalWidth;
        if (IsTemplateMode)
        {
            totalWidth = DefaultTilemapWidth;
        }
        else
        {
            totalWidth = 0f;
            foreach (var pattern in _currentLevelInfo.patterns)
            {
                totalWidth += GetPatternDisplayWidth(pattern);
            }
            if (totalWidth <= 0f)
                totalWidth = DefaultTilemapWidth;
        }

        // Создаём сцену с фоном и дорогой по naming convention
        string locationForBg = IsTemplateMode ? Consts.TemplatesFallbackLocation : _currentLocationName;
        string daypartSlug = IsTemplateMode ? "morning" : _selectedDaypart.ToString().ToLowerInvariant();
        var tilemapGameObject = SceneCreator.CreateSceneWithTilemap((int)totalWidth, locationForBg, daypartSlug);
        _tilemapInScene = tilemapGameObject.GetComponent<Tilemap>();

        // Load patterns (obstacles) for both Templates and Locations
        if (_currentLevelInfo.patterns.Count == 0)
        {
            Debug.LogWarning("Уровень не содержит паттернов.");
            _uiManager.UpdatePatternsList(new List<string>());
            _selectedPatternIndex = -1;
            
            // For locations without patterns, still load decorations
            if (!IsTemplateMode)
            {
                LoadDecorationsToTilemap();
            }
            
            return;
        }

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();
        _uiManager.UpdatePatternsList(patternNames);

        if (IsTemplateMode)
        {
            _uiManager.SelectFirstPattern();
        }
        else
        {
            RenderAllPatternsToTilemap();
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

        // Map filtered index to real index when search filter is active
        if (IsTemplateMode && _filteredPatternIndices.Count > 0 && selectedIndex >= 0 && selectedIndex < _filteredPatternIndices.Count)
        {
            _selectedPatternIndex = _filteredPatternIndices[selectedIndex];
        }
        else
        {
            _selectedPatternIndex = selectedIndex;
        }

        if (CurrentPattern == null)
        {
            Debug.LogWarning("Выбранный паттерн некорректен.");
            return;
        }

        _uiManager.UpdatePatternNameField(CurrentPattern.name);
        _uiManager.UpdatePatternDescriptionField(CurrentPattern.desсription);

        AddTilesToTilemap();

        if (IsTemplateMode)
        {
            FrameCurrentTilemapBounds();
        }
    }

    /// <summary>
    /// Изменение флажка, указывающего, что объект размещается на крыше.
    /// </summary>
    private void HandleIsObjectOnRoofToggleChanged(bool newValue)
    {
        _isObjectOnRoof = newValue;
        Debug.Log($"[LevelTilemapEditor] IsObjectOnRoof state changed: {_isObjectOnRoof}");
    }

    /// <summary>
    /// Очищает тайлмап и расставляет тайлы из текущего паттерна.
    /// </summary>
    private void AddTilesToTilemap()
    {
        if (CurrentPattern == null || _tilemapInScene == null)
            return;

        _isTilemapBulkOperation = true;
        _tilemapInScene.ClearAllTiles();

        var positions = new List<Vector3Int>();
        var tiles = new List<TileBase>();
        var worldPositions = new List<Vector3>();

        foreach (var obstacle in CurrentPattern.obstacles)
        {
            var loadedSprite = SpriteLoader.LoadSpriteSync(obstacle.spriteName);
            if (loadedSprite != null)
            {
                var tile = CreateInstance<Tile>();
                tile.sprite = loadedSprite;
                tile.name = obstacle.spriteName;

                var worldPos = new Vector3(obstacle.x, obstacle.y, 0f);
                var cellPos = _tilemapInScene.WorldToCell(worldPos);

                positions.Add(cellPos);
                tiles.Add(tile);
                worldPositions.Add(worldPos);
            }
            else
            {
                Debug.LogWarning($"Не удалось загрузить спрайт: {obstacle.spriteName}");
            }
        }

        _tilemapInScene.SetTiles(positions.ToArray(), tiles.ToArray());

        for (int i = 0; i < positions.Count; i++)
        {
            ApplyExactTileWorldPosition(_tilemapInScene, positions[i], worldPositions[i]);
        }

        // Restore decorations after clearing tilemap (they are level-wide, not per-pattern)
        if (!IsTemplateMode)
        {
            LoadDecorationsToTilemap();
        }

        EditorUtility.SetDirty(_tilemapInScene.gameObject);
        _isTilemapBulkOperation = false;
    }

    /// <summary>
    /// Отрисовывает все паттерны уровня последовательно слева направо (Level mode).
    /// Ширина каждого паттерна определяется реальным диапазоном x-координат его obstacles.
    /// </summary>
    private void RenderAllPatternsToTilemap()
    {
        if (_currentLevelInfo == null || _tilemapInScene == null)
            return;

        _isTilemapBulkOperation = true;
        _tilemapInScene.ClearAllTiles();
        _cellToPatternMap.Clear();
        _patternBounds.Clear();
        _patternOverlaySlots.Clear();

        var positions = new List<Vector3Int>();
        var tiles = new List<TileBase>();
        var worldPositions = new List<Vector3>();
        float cumulativeOffset = 0f;

        for (int p = 0; p < _currentLevelInfo.patterns.Count; p++)
        {
            var pattern = _currentLevelInfo.patterns[p];
            float patternWidth = GetPatternDisplayWidth(pattern);
            float slotStartX = cumulativeOffset;
            _patternOverlaySlots.Add(new PatternOverlaySlot
            {
                PatternIndex = p,
                LeftX = slotStartX,
                RightX = slotStartX + patternWidth,
                Name = pattern.name,
                IsRelief = string.Equals(pattern.name, "relief", StringComparison.OrdinalIgnoreCase)
            });

            if (pattern.obstacles == null || pattern.obstacles.Count == 0)
            {
                _patternBounds.Add(CreatePatternFrameBounds(new Bounds(), false, slotStartX, patternWidth));
                cumulativeOffset += patternWidth;
                continue;
            }

            ComputePatternXRange(pattern, out float minX, out _);
            float patternOffset = cumulativeOffset - minX;

            bool boundsInitialized = false;
            var patternBounds = new Bounds();

            for (int o = 0; o < pattern.obstacles.Count; o++)
            {
                var obstacle = pattern.obstacles[o];
                if (string.IsNullOrWhiteSpace(obstacle.spriteName))
                {
                    Debug.LogWarning($"[LevelTilemapEditor] Obstacle without spriteName in pattern '{pattern.name}' (id={o}, type={obstacle.type}).");
                    continue;
                }

                var sprite = SpriteLoader.LoadSpriteSync(obstacle.spriteName);
                if (sprite == null) continue;

                var tile = CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.name = obstacle.spriteName;

                var worldPos = new Vector3(obstacle.x + patternOffset, obstacle.y, 0f);
                var cellPos = _tilemapInScene.WorldToCell(worldPos);

                // Учитываем sprite pivot/center: worldPos не всегда совпадает с геометрическим центром спрайта.
                var spriteBounds = BuildSpriteWorldBounds(sprite, worldPos);
                if (!boundsInitialized)
                {
                    patternBounds = spriteBounds;
                    boundsInitialized = true;
                }
                else
                {
                    patternBounds.Encapsulate(spriteBounds);
                }

                positions.Add(cellPos);
                tiles.Add(tile);
                worldPositions.Add(worldPos);
                _cellToPatternMap[cellPos] = (p, o);
            }

            _patternBounds.Add(CreatePatternFrameBounds(patternBounds, boundsInitialized, slotStartX, patternWidth));
            cumulativeOffset += patternWidth;
        }

        _tilemapInScene.SetTiles(positions.ToArray(), tiles.ToArray());

        for (int i = 0; i < positions.Count; i++)
        {
            ApplyExactTileWorldPosition(_tilemapInScene, positions[i], worldPositions[i]);
        }

        LoadDecorationsToTilemap();

        EditorUtility.SetDirty(_tilemapInScene.gameObject);
        _isTilemapBulkOperation = false;
    }

    private static void ApplyExactTileWorldPosition(Tilemap tilemap, Vector3Int cellPos, Vector3 worldPos)
    {
        var cellWorldPos = tilemap.CellToWorld(cellPos);
        var localOffset = worldPos - cellWorldPos;
        localOffset.z = GetRoadTileZOffset(worldPos.y);
        var matrix = Matrix4x4.Translate(localOffset);
        tilemap.SetTransformMatrix(cellPos, matrix);
    }

    private static float GetRoadTileZOffset(float yPosition)
    {
        return ObstacleLaneResolver.IsBottomLineCloser(yPosition) ? LowerRoadTileZOffset : 0f;
    }

    /// <summary>
    /// Возвращает фактическую world-позицию тайла с учётом transform matrix клетки.
    /// </summary>
    private static Vector3 GetExactTileWorldPosition(Tilemap tilemap, Vector3Int cellPos)
    {
        return TilemapPositionUtility.GetExactTileWorldPosition(tilemap, cellPos);
    }

    /// <summary>
    /// Зумирует SceneView к области выбранного паттерна.
    /// </summary>
    private void ZoomToPattern(int patternIndex)
    {
        if (patternIndex < 0 || patternIndex >= _patternBounds.Count)
            return;

        var bounds = _patternBounds[patternIndex];
        FrameToBounds(bounds);
    }

    /// <summary>
    /// Вычисляет максимальную ширину среди всех паттернов (по obstacle.x).
    /// </summary>
    private float ComputeMaxPatternWidth(List<Pattern> patterns)
    {
        float maxWidth = 0f;
        foreach (var pattern in patterns)
        {
            float width = GetPatternDisplayWidth(pattern);
            if (width > maxWidth) maxWidth = width;
        }
        return maxWidth;
    }

    /// <summary>
    /// Вычисляет bounds всех тайлов на Tilemap и зумирует SceneView к ним.
    /// </summary>
    private void FrameCurrentTilemapBounds()
    {
        if (_tilemapInScene == null) return;

        if (!TryGetExactTilemapBounds(_tilemapInScene, out var bounds))
            return;

        FrameToBounds(bounds);
    }

    private static float GetPatternDisplayWidth(Pattern pattern)
    {
        if (pattern?.obstacles == null || pattern.obstacles.Count == 0)
            return PatternGap;

        ComputePatternXRange(pattern, out float minX, out float maxX);

        return maxX - minX + PatternGap;
    }

    private static void ComputePatternXRange(Pattern pattern, out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;

        if (pattern?.obstacles == null || pattern.obstacles.Count == 0)
        {
            minX = 0f;
            maxX = 0f;
            return;
        }

        for (int i = 0; i < pattern.obstacles.Count; i++)
        {
            var obstacle = pattern.obstacles[i];
            float leftX = obstacle.x;
            float rightX = obstacle.x;

            if (!string.IsNullOrEmpty(obstacle.spriteName))
            {
                var sprite = SpriteLoader.LoadSpriteSync(obstacle.spriteName);
                if (sprite != null)
                {
                    var center = sprite.bounds.center.x;
                    var extent = sprite.bounds.extents.x;
                    leftX = obstacle.x + center - extent;
                    rightX = obstacle.x + center + extent;
                }
            }

            if (leftX < minX) minX = leftX;
            if (rightX > maxX) maxX = rightX;
        }

        if (minX == float.MaxValue || maxX == float.MinValue)
        {
            minX = 0f;
            maxX = 0f;
        }
    }

    private static Bounds CreatePatternFrameBounds(Bounds spriteBounds, bool hasSpriteBounds, float slotStartX, float patternWidth)
    {
        var slotBounds = new Bounds(
            new Vector3(slotStartX + patternWidth * 0.5f, DefaultPatternFrameCenterY, 0f),
            new Vector3(Mathf.Max(patternWidth, MinPatternFrameWidth), MinPatternFrameHeight, 0f));

        if (!hasSpriteBounds)
            return slotBounds;

        slotBounds.Encapsulate(spriteBounds);
        slotBounds.Expand(new Vector3(PatternFrameHorizontalPadding * 2f, PatternFrameVerticalPadding * 2f, 0f));

        if (slotBounds.size.x < MinPatternFrameWidth)
        {
            slotBounds.size = new Vector3(MinPatternFrameWidth, slotBounds.size.y, slotBounds.size.z);
        }

        if (slotBounds.size.y < MinPatternFrameHeight)
        {
            slotBounds.size = new Vector3(slotBounds.size.x, MinPatternFrameHeight, slotBounds.size.z);
        }

        return slotBounds;
    }

    private static bool TryGetExactTilemapBounds(Tilemap tilemap, out Bounds bounds)
    {
        bounds = default;

        var cellBounds = tilemap.cellBounds;
        bool initialized = false;
        foreach (var cellPos in cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cellPos))
                continue;

            var sprite = tilemap.GetSprite(cellPos);
            if (sprite == null)
                continue;

            var worldPos = GetExactTileWorldPosition(tilemap, cellPos);
            var spriteBounds = BuildSpriteWorldBounds(sprite, worldPos);
            if (!initialized)
            {
                bounds = spriteBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(spriteBounds);
            }
        }

        if (!initialized)
            return false;

        bounds.Expand(new Vector3(PatternFrameHorizontalPadding * 2f, PatternFrameVerticalPadding * 2f, 0f));
        return true;
    }

    private static Bounds BuildSpriteWorldBounds(Sprite sprite, Vector3 worldPos)
    {
        var localCenter = sprite.bounds.center;
        var worldCenter = new Vector3(worldPos.x + localCenter.x, worldPos.y + localCenter.y, worldPos.z + localCenter.z);
        return new Bounds(worldCenter, new Vector3(sprite.bounds.size.x, sprite.bounds.size.y, 0f));
    }

    /// <summary>
    /// Зумирует SceneView, чтобы указанные bounds заполняли viewport.
    /// </summary>
    private void FrameToBounds(Bounds bounds)
    {
        if (bounds.size == Vector3.zero) return;

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        EditorApplication.delayCall += () =>
        {
            sceneView.Frame(bounds, false);

            float aspect = sceneView.camera.aspect;
            float sizeByWidth = bounds.size.x / (2f * aspect);
            float sizeByHeight = bounds.size.y / 2f;
            sceneView.size = Mathf.Max(sizeByWidth, sizeByHeight);
            sceneView.Repaint();
        };
    }

    /// <summary>
    /// Обработка кликов мыши в SceneView для выбора obstacle и показа override panel.
    /// </summary>
    private void OnSceneGUI(SceneView sceneView)
    {
        if (IsTemplateMode)
            return;

        DrawPatternBoundsOverlay();

        if (_tilemapInScene == null || _cellToPatternMap.Count == 0)
            return;

        var evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || evt.alt)
            return;

        var worldRay = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        var worldPos = worldRay.origin;
        worldPos.z = 0f;

        var cellPos = _tilemapInScene.WorldToCell(worldPos);
        if (!_cellToPatternMap.TryGetValue(cellPos, out var mapping))
            return;

        var (patternIndex, obstacleIndex) = mapping;
        HandleTileClicked(patternIndex, obstacleIndex);
        evt.Use();
    }

    private void DrawPatternBoundsOverlay()
    {
        if (_patternOverlaySlots.Count == 0)
            return;

        if (_currentLevelInfo?.patterns == null)
            return;

        if (!TryGetExactTilemapBounds(_tilemapInScene, out var worldBounds))
            return;

        float minY = worldBounds.min.y - 0.35f;
        float maxY = worldBounds.max.y + 0.35f;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        for (int i = 0; i < _patternOverlaySlots.Count; i++)
        {
            var slot = _patternOverlaySlots[i];
            if (slot.IsRelief)
                continue;

            bool isSelected = slot.PatternIndex == _selectedPatternIndex;
            var borderColor = isSelected ? SelectedPatternBoundaryColor : PatternBoundaryColor;
            float lineThickness = isSelected ? PatternBoundaryLineThickness + 0.5f : PatternBoundaryLineThickness;

            float leftX = slot.LeftX - PatternBoundaryInset;
            float rightX = slot.RightX + PatternBoundaryInset;

            var bottomLeft = new Vector3(leftX, minY, 0f);
            var topLeft = new Vector3(leftX, maxY, 0f);
            var bottomRight = new Vector3(rightX, minY, 0f);
            var topRight = new Vector3(rightX, maxY, 0f);

            Handles.color = borderColor;
            Handles.DrawAAPolyLine(lineThickness, bottomLeft, topLeft);
            Handles.DrawAAPolyLine(lineThickness, bottomRight, topRight);

            if (isSelected)
            {
                string label = $"{slot.PatternIndex + 1}: {slot.Name}";
                var labelPos = new Vector3((leftX + rightX) * 0.5f, maxY + 0.2f, 0f);
                Handles.Label(labelPos, label);
            }
        }

        Handles.color = Color.white;
    }

    /// <summary>
    /// Показывает override panel для кликнутого obstacle.
    /// </summary>
    private void HandleTileClicked(int patternIndex, int obstacleIndex)
    {
        if (_currentLevelRef == null || _patternsCollection == null || _locationTheme == null)
            return;

        if (patternIndex < 0 || patternIndex >= _currentLevelRef.patternSequence.Count)
            return;

        var patternRef = _currentLevelRef.patternSequence[patternIndex];
        var template = _patternsCollection.patterns.Find(p => p.name == patternRef.@ref);
        if (template == null || obstacleIndex < 0 || obstacleIndex >= template.obstacles.Count)
            return;

        var slot = template.obstacles[obstacleIndex];
        var resolvedSpriteName = _currentLevelInfo.patterns[patternIndex].obstacles[obstacleIndex].spriteName;

        // Determine source
        var hasOverride = patternRef.overrides?.Exists(o => o.obstacleId == slot.id) == true;
        var source = hasOverride ? "override" : (patternRef.spriteSeed != 0 ? "seed" : "theme");

        _spriteOverridePanel.Show(_currentLevelRef, _locationTheme, patternIndex, slot, resolvedSpriteName, source);
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
        if (_currentLevelInfo == null)
        {
            Debug.LogWarning("Невозможно добавить паттерн: информация об уровне отсутствует.");
            return;
        }

        var newPattern = new Pattern
        {
            name = GenerateNewPatternName(),
            desсription = string.Empty,
            obstacles = new List<ObstacleModel>()
        };

        _currentLevelInfo.patterns.Add(newPattern);

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        _selectedPatternIndex = _currentLevelInfo.patterns.Count - 1;
        _uiManager.UpdatePatternsList(patternNames, _selectedPatternIndex);
        _uiManager.UpdatePatternNameField(newPattern.name);
        _uiManager.UpdatePatternDescriptionField(newPattern.desсription);

        Debug.Log($"Добавлен новый паттерн: {newPattern.name}");
    }

    private string GenerateNewPatternName()
    {
        var existingNames = new HashSet<string>(
            _currentLevelInfo.patterns
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern?.name))
                .Select(pattern => pattern.name),
            StringComparer.OrdinalIgnoreCase);

        var basePatternName = CurrentPattern?.name;
        var candidateName = string.IsNullOrWhiteSpace(basePatternName)
            ? "Pattern 01"
            : GetIncrementedPatternName(basePatternName);

        while (existingNames.Contains(candidateName))
        {
            candidateName = GetIncrementedPatternName(candidateName);
        }

        return candidateName;
    }

    private static string GetIncrementedPatternName(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return "Pattern 01";

        var match = PatternNameSuffixRegex.Match(sourceName.Trim());
        if (!match.Success)
            return $"{sourceName.Trim()}_1";

        var prefix = match.Groups[1].Value;
        var digits = match.Groups[2].Value;
        var incrementedValue = (int.Parse(digits) + 1).ToString(new string('0', digits.Length));
        return prefix + incrementedValue;
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
        if (_currentLevelInfo == null ||
            _selectedPatternIndex < 0 ||
            _selectedPatternIndex >= _currentLevelInfo.patterns.Count)
        {
            return;
        }

        var selectedIndex = _selectedPatternIndex;
        var removedDisplayedIndex = string.IsNullOrEmpty(_patternSearchFilter)
            ? selectedIndex
            : Math.Max(_filteredPatternIndices.IndexOf(selectedIndex), 0);
        var patternToRemove = _currentLevelInfo.patterns[selectedIndex];

        _currentLevelInfo.patterns.RemoveAt(selectedIndex);
        RefreshFilteredPatternCache();

        var visiblePatternNames = _filteredPatternIndices
            .Select(i => _allPatternNames[i])
            .ToList();

        if (visiblePatternNames.Count == 0)
        {
            _selectedPatternIndex = -1;
            _uiManager.UpdatePatternsList(visiblePatternNames, -1);
            ClearSelectedPatternView();
            Debug.Log($"Удален паттерн: {patternToRemove.name}");
            return;
        }

        var selectedDisplayedIndex = Math.Min(removedDisplayedIndex, visiblePatternNames.Count - 1);
        _selectedPatternIndex = _filteredPatternIndices[selectedDisplayedIndex];

        _uiManager.UpdatePatternsList(visiblePatternNames, selectedDisplayedIndex);
        SyncSelectedPatternView();

        Debug.Log($"Удален паттерн: {patternToRemove.name}");
    }

    private void ClearSelectedPatternView()
    {
        _uiManager.UpdatePatternNameField(string.Empty);
        _uiManager.UpdatePatternDescriptionField(string.Empty);

        if (_tilemapInScene == null)
        {
            return;
        }

        _isTilemapBulkOperation = true;
        _tilemapInScene.ClearAllTiles();
        _isTilemapBulkOperation = false;

        _cellToPatternMap.Clear();
        _patternBounds.Clear();
        _patternOverlaySlots.Clear();
    }

    private void SyncSelectedPatternView()
    {
        if (CurrentPattern == null)
        {
            ClearSelectedPatternView();
            return;
        }

        _uiManager.UpdatePatternNameField(CurrentPattern.name);
        _uiManager.UpdatePatternDescriptionField(CurrentPattern.desсription);
        AddTilesToTilemap();

        if (IsTemplateMode)
        {
            FrameCurrentTilemapBounds();
        }
    }

    /// <summary>
    /// Обновляет список спрайтов в текущей локации.
    /// </summary>
    private void UpdateSpritesInfoInCurrentLocation(string newValue)
    {
        var effectiveLocation = string.Equals(newValue, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase)
            ? Consts.TemplatesFallbackLocation
            : newValue;
        _spritesDirectory = Path.Combine(Consts.LocationsPath, effectiveLocation, "sprites");
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

        _tilemapInScene = null;
        _selectedFile = null;
        _currentLevelInfo = null;
        _currentLevelRef = null;
        _selectedPatternIndex = -1;
        _selectedLevelDescriptor = null;

        _uiManager.UpdatePatternsList(new List<string>());
        _patternSequencePanel.Hide();
        _spriteOverridePanel.Hide();
    }

    /// <summary>
    /// Деактивирует редактор: восстанавливает исходную сцену и пересоздаёт UI в idle-состоянии.
    /// Повторная активация произойдёт при выборе локации.
    /// </summary>
    private void HandleResetClicked()
    {
        UnsubscribeEvents();
        Deactivate();

        rootVisualElement.Clear();
        CreateGUI();
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
            SavePatternsCollectionToDisk();
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

    private void SavePatternsCollectionToDisk()
    {
        if (_patternsCollection == null || _currentLevelInfo == null)
        {
            Debug.LogWarning("Cannot save PatternsCollection: template data is not initialized.");
            return;
        }

        SyncTemplatesFromLevelInfo();

        var pcPath = Path.Combine(Consts.LocationsPath, Consts.TemplatesLocationName, "levels", "PatternsCollection.json");
        var json = JsonUtility.ToJson(_patternsCollection, true);
        File.WriteAllText(pcPath, json, System.Text.Encoding.UTF8);
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
    /// Фильтрация списка паттернов по поисковому запросу (Templates mode).
    /// </summary>
    private void HandlePatternSearchChanged(string searchText)
    {
        _patternSearchFilter = searchText ?? "";
        RefreshFilteredPatternsList();
    }

    /// <summary>
    /// Обновляет отфильтрованный список паттернов и UI. Сохраняет выбор, если возможно.
    /// </summary>
    private void RefreshFilteredPatternsList()
    {
        if (_currentLevelInfo == null) return;

        RefreshFilteredPatternCache();

        var filteredNames = _filteredPatternIndices.Select(i => _allPatternNames[i]).ToList();
        _uiManager.UpdatePatternsList(filteredNames);

        // Preserve selection if possible
        if (_selectedPatternIndex >= 0)
        {
            var posInFiltered = _filteredPatternIndices.IndexOf(_selectedPatternIndex);
            if (posInFiltered >= 0)
            {
                _uiManager.SelectPatternByIndex(posInFiltered);
                return;
            }
        }

        // Select first filtered if current is not visible
        if (_filteredPatternIndices.Count > 0)
            _uiManager.SelectFirstPattern();
    }

    private void RefreshFilteredPatternCache()
    {
        if (_currentLevelInfo == null)
        {
            _allPatternNames.Clear();
            _filteredPatternIndices.Clear();
            return;
        }

        _allPatternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        if (string.IsNullOrEmpty(_patternSearchFilter))
        {
            _filteredPatternIndices = Enumerable.Range(0, _allPatternNames.Count).ToList();
        }
        else
        {
            _filteredPatternIndices = new List<int>();
            for (int i = 0; i < _allPatternNames.Count; i++)
            {
                if (_allPatternNames[i].IndexOf(_patternSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    _filteredPatternIndices.Add(i);
            }
        }
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
        _uiManager.OnIsObjectOnRoofToggleChanged += HandleIsObjectOnRoofToggleChanged;
        _uiManager.OnResetClicked += HandleResetClicked;
        _uiManager.OnPatternNameChanged += HandlePatternNameChanged;
        _uiManager.OnPatternDescriptionChanged += HandlePatternDescriptionChanged;
        _uiManager.OnDaypartChanged += HandleDaypartChanged;
        _uiManager.OnPatternSearchChanged += HandlePatternSearchChanged;

        _patternSequencePanel.OnSequenceChanged += HandlePatternSequenceChanged;
        _patternSequencePanel.OnPatternSelected += ZoomToPattern;
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
            _uiManager.OnIsObjectOnRoofToggleChanged -= HandleIsObjectOnRoofToggleChanged;
            _uiManager.OnResetClicked -= HandleResetClicked;
            _uiManager.OnPatternNameChanged -= HandlePatternNameChanged;
            _uiManager.OnPatternDescriptionChanged -= HandlePatternDescriptionChanged;
            _uiManager.OnDaypartChanged -= HandleDaypartChanged;
            _uiManager.OnPatternSearchChanged -= HandlePatternSearchChanged;
        }

        if (_patternSequencePanel != null)
        {
            _patternSequencePanel.OnSequenceChanged -= HandlePatternSequenceChanged;
            _patternSequencePanel.OnPatternSelected -= ZoomToPattern;
        }
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

        float totalWidth = 0f;
        foreach (var pattern in _currentLevelInfo.patterns)
            totalWidth += GetPatternDisplayWidth(pattern);
        if (totalWidth <= 0f)
            totalWidth = DefaultTilemapWidth;

        var daypartSlug = _selectedDaypart.ToString().ToLowerInvariant();
        var tilemapGameObject = SceneCreator.CreateSceneWithTilemap((int)totalWidth, _currentLocationName, daypartSlug);
        _tilemapInScene = tilemapGameObject.GetComponent<Tilemap>();

        RenderAllPatternsToTilemap();
    }

    /// <summary>
    /// Re-resolves and refreshes current pattern when a sprite override changes.
    /// </summary>
    private void HandleSpriteOverrideChanged()
    {
        if (_currentLevelRef == null || _patternsCollection == null || _locationTheme == null)
            return;

        _currentLevelInfo = LevelResolver.Resolve(_currentLevelRef, _patternsCollection, _locationTheme);
        RenderAllPatternsToTilemap();
    }

    /// <summary>
    /// Converts PatternsCollection to LevelInfo for tilemap display in Templates mode.
    /// Uses the given theme to resolve sprite names for display purposes.
    /// </summary>
    private static LevelInfo ResolveTemplatesForDisplay(PatternsCollection pc, LocationTheme theme)
    {
        var levelRef = new LevelInfoRef
        {
            location = Consts.TemplatesFallbackLocation
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

        var syncedTemplates = new List<PatternTemplate>(_currentLevelInfo.patterns.Count);
        for (int index = 0; index < _currentLevelInfo.patterns.Count; index++)
        {
            var existingTemplate = index < _patternsCollection.patterns.Count
                ? _patternsCollection.patterns[index]
                : new PatternTemplate();
            syncedTemplates.Add(BuildTemplateFromResolvedPattern(_currentLevelInfo.patterns[index], existingTemplate));
        }

        _patternsCollection.patterns = syncedTemplates;
    }

    private static PatternTemplate BuildTemplateFromResolvedPattern(Pattern resolvedPattern, PatternTemplate template)
    {
        template ??= new PatternTemplate();

        var existingObstacles = template.obstacles ?? new List<ObstacleSlot>();
        var newObstacles = new List<ObstacleSlot>();
        int nextId = Math.Max(template.nextObstacleId, 0);

        if (resolvedPattern?.obstacles != null)
        {
            foreach (var obstacle in resolvedPattern.obstacles)
            {
                var existingSlot = existingObstacles.Find(s =>
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
        }

        template.obstacles = newObstacles;
        template.nextObstacleId = nextId;
        template.name = resolvedPattern?.name ?? string.Empty;
        template.description = resolvedPattern?.desсription ?? string.Empty;
        return template;
    }
}
