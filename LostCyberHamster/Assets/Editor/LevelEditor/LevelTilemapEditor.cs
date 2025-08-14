using Assets.Scripts.Common.Models;
using Assets.Scripts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
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
    private string _selectedFile;
    private int _selectedPatternIndex = -1;
    private Tilemap _tipeMapInScene;
    private Pattern _currentPattern;
    private bool _isCollectableOnRoof;
    private bool _isTilemapBulkOperation;

    private string _levelsDirectory;
    private string _levelDesignTemplatesDirectory;
    private string _spritesDirectory;
    private string _backgroundsPath;
    private string _currentLocationName;
    private List<string> _spritesNames { get; set; }

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
        SubscribeEvents();
        InitializePatternButtons();
        InitializeLevelDesignTemplateDirectory();
    }

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
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
    /// Сохраняет текущий уровень.
    /// </summary>
    private void HandleSaveLevelClicked()
    {
        if (_currentLocationName != Consts.TemplatesLocationName)
        {
            Debug.Log("Cannot save level: only available for level_design_templates location");
            return;
        }

        Debug.Log("Сохранение уровня");
        LevelDataManager.SaveLevel(_currentLevelInfo, _selectedFile);
    }

    /// <summary>
    /// Создаёт новый уровень с выбранным фоном.
    /// </summary>
    private void HandleCreateLevelClicked()
    {
        var newLevelInfo = new LevelInfo
        {
            backgroundTexture = _uiManager.BackGroundDropdown.value
        };

        if(_uiManager.IsTemplateMode)
        {
            LevelDataManager.CreateNewTemplate(newLevelInfo, _uiManager.TemplateLevelName, _levelsDirectory, _spritesNames);
        }
        else
        {
            LevelDataManager.CreateNewLevel(newLevelInfo, _levelsDirectory, _spritesNames);
        }

       
        AssetDatabase.Refresh();
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

        _levelsDirectory = Path.Combine(Consts.LocationsPath, newValue, "levels");
        UpdateSpritesInfoInCurrentLocation(newValue);

        UpdateBackgroundDropdown();
        var levelFiles = LevelDataManager.GetLevelFiles(_levelsDirectory, _levelsExt);
        _uiManager.UpdateFilesList(levelFiles);

        // Автоматически выбираем первый файл, если он есть
        if (levelFiles.Count > 0)
        {
            _uiManager.SelectFirstFile();
        }
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
    /// Обработка выбора файла уровня из списка.
    /// </summary>
    private void HandleFileSelected(string selectedFile)
    {
        SpriteLoader.ReleaseSpritesAndClearCache();

        _selectedFile = selectedFile;

        if (string.IsNullOrEmpty(_selectedFile))
        {
            Debug.LogWarning("Не выбран файл уровня.");
            return;
        }

        _currentLevelInfo = LevelDataManager.LoadLevel(_selectedFile);
        if (_currentLevelInfo == null)
        {
            Debug.LogWarning($"Не удалось загрузить уровень из файла {_selectedFile}");
            return;
        }

        LevelDataManager.FixObstacleTypesInLevelInfoAndSaveToJson(_currentLocationName, _selectedFile, _currentLevelInfo);

        // 1) Вычисляем ширину для одной "прокрутки" паттерна
        //    Исходя из того, что при ScrollSpeed=1 движемся ~3.8 ед/сек.
        //    5 минут = 300 секунд => 300 * 3.8 = 1140, например.
        float totalWidth = _patternDurationMinutes * 60f * 3.8f;

        // Вместо создания новой сцены — вызываем обновлённый метод,
        // который сам найдёт/очистит/или создаст сцену, и вернёт TilemapGameObject.
        var tilemapGameObject = SceneCreator.CreateSceneWithTilemap((int)totalWidth, _currentLevelInfo);
        _tipeMapInScene = tilemapGameObject.GetComponent<Tilemap>();

        var patternCount = _currentLevelInfo.patterns.Count;
        if (patternCount == 0)
        {
            Debug.LogWarning("Уровень не содержит паттернов.");
            _uiManager.UpdatePatternsList(new List<string>());
            _currentPattern = null;
            _selectedPatternIndex = -1;
            return;
        }

        var patternNames = _currentLevelInfo.patterns.Select(p => p.name).ToList();

        _uiManager.UpdatePatternsList(patternNames);
        _uiManager.SelectFirstPattern();
        UpdateBackgroundDropdown();
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
        var moveUpButton = rootVisualElement.Q<Button>("move-up-btn");
        var moveDownButton = rootVisualElement.Q<Button>("move-down-btn");

        moveUpButton.clicked += MovePatternUp;
        moveDownButton.clicked += MovePatternDown;
        addPatternButton.clicked += AddNewPattern;
        removePatternButton.clicked += RemovePattern;
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
        _backgroundsPath = Path.Combine(Consts.LocationsPath, newValue, "sprites", "backgrounds");
        var sprites = Directory.GetFiles(_spritesDirectory, $"*.{_spritesExt}", SearchOption.AllDirectories);

        _spritesNames = sprites.Select(Path.GetFileNameWithoutExtension).ToList();
    }

    /// <summary>
    /// Обновляет выпадающий список фонов.
    /// </summary>
    private void UpdateBackgroundDropdown()
    {
        _uiManager.BackGroundDropdown.choices.Clear();

        if (!Directory.Exists(_backgroundsPath))
        {
            Debug.LogWarning($"Background path '{_backgroundsPath}' does not exist.");
            return;
        }

        var textureFiles = Directory.GetFiles(_backgroundsPath, "*.png").ToArray();
        var textureNames = textureFiles.Select(Path.GetFileNameWithoutExtension).ToList();

        _uiManager.BackGroundDropdown.choices.AddRange(textureNames);

        var defaultValue = string.Empty;
        if (_currentLevelInfo != null && !string.IsNullOrEmpty(_currentLevelInfo.backgroundTexture))
        {
            if (textureNames.Contains(_currentLevelInfo.backgroundTexture))
            {
                defaultValue = _currentLevelInfo.backgroundTexture;
            }
        }

        _uiManager.BackGroundDropdown.value = defaultValue;
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

        SpriteLoader.ReleaseSpritesAndClearCache();

        rootVisualElement.Clear();
        CreateGUI();
    }

    private void HandleBackgroundSelected(string backgroundName)
    {
        if (_currentLevelInfo == null)
        {
            Debug.LogWarning("Current level info is not initialized.");
            return;
        }
        _currentLevelInfo.backgroundTexture = backgroundName;
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
        _uiManager.OnBackgroundSelected += HandleBackgroundSelected;
        _uiManager.OnPatternDurationChanged += HandlePatternDurationChanged;
        _uiManager.OnPatternNameChanged += HandlePatternNameChanged;
        _uiManager.OnPatternDescriptionChanged += HandlePatternDescriptionChanged;


        Tilemap.tilemapTileChanged += OnTileChanged;
    }

    /// <summary>
    /// Отписка от событий UI и Tilemap.
    /// </summary>
    private void UnsubscribeEvents()
    {
        _uiManager.OnCreateLevelClicked -= HandleCreateLevelClicked;
        _uiManager.OnSaveLevelClicked -= HandleSaveLevelClicked;
        _uiManager.OnLocationChanged -= HandleLocationChanged;
        _uiManager.OnSpriteSelected -= HandleSpriteSelected;
        _uiManager.OnFileSelected -= HandleFileSelected;
        _uiManager.OnPatternSelected -= HandlePatternSelected;
        _uiManager.OnIsCollectableOnRoofToggleChanged -= HandleIsCollectableOnRoofToggleChanged;
        _uiManager.OnResetClicked -= HandleResetClicked;
        _uiManager.OnBackgroundSelected -= HandleBackgroundSelected;
        _uiManager.OnPatternDurationChanged -= HandlePatternDurationChanged;
        _uiManager.OnPatternNameChanged -= HandlePatternNameChanged;
        _uiManager.OnPatternDescriptionChanged -= HandlePatternDescriptionChanged;


        Tilemap.tilemapTileChanged -= OnTileChanged;
    }
}
