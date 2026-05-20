using Assets.Scripts.Common.Models;
using Assets.Scripts.System.LevelManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.AddressableAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Assets.Editor.LevelEditor;
using Assets.Editor.LevelEditor.ObstacleSpriteTypeMappingManagement;
using Assets.Scripts;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Text.RegularExpressions;

public static class LevelDataManager
{
    private const string MappingFileName   = "obstacle_sprite_to_type_mappings";
    private const string MappingsGroupName = "Mappings";
    private static readonly Regex _levelKeySanitizer = new Regex(@"[^a-z0-9_]+", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, PartOfDayEnum> _partOfDayFolderMap =
        new Dictionary<string, PartOfDayEnum>(StringComparer.OrdinalIgnoreCase)
        {
            { "Morning",  PartOfDayEnum.Morning },
            { "Afternoon", PartOfDayEnum.Afternoon },
            { "Evening",  PartOfDayEnum.Evening },
            { "Night",    PartOfDayEnum.Night }
        };

    public static IReadOnlyList<LevelFileDescriptor> GetLevelFileDescriptors(string levelsDirectory,
        string extension = "json")
    {
        if (string.IsNullOrWhiteSpace(levelsDirectory))
        {
            Debug.LogError("Levels directory path is null or empty.");
            return Array.Empty<LevelFileDescriptor>();
        }

        if (!Directory.Exists(levelsDirectory))
        {
            Debug.LogError($"Directory does not exist: {levelsDirectory}");
            return Array.Empty<LevelFileDescriptor>();
        }

        var descriptors = new List<LevelFileDescriptor>();
        var files = EnumerateLevelFiles(levelsDirectory, extension);

        foreach (var absolutePath in files)
        {
            var relativePath = Path.GetRelativePath(levelsDirectory, absolutePath);
            relativePath = NormalizePath(relativePath);

            var partOfDay = ResolvePartOfDay(relativePath);
            var displayName = BuildDisplayName(relativePath, partOfDay);

            descriptors.Add(new LevelFileDescriptor(absolutePath, relativePath, partOfDay, displayName));
        }

        descriptors.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return descriptors;
    }

    public static IReadOnlyList<LevelFileDescriptor> GetLevelFileDescriptors(
        string levelsDirectory,
        PartOfDayEnum partOfDay,
        string extension = "json")
    {
        var descriptors = GetLevelFileDescriptors(levelsDirectory, extension);
        if (descriptors.Count == 0)
        {
            return descriptors;
        }

        return descriptors
            .Where(descriptor => descriptor.PartOfDay.HasValue && descriptor.PartOfDay.Value == partOfDay)
            .ToList();
    }

    private static string BuildDisplayName(string relativePath, PartOfDayEnum? partOfDay)
    {
        if (!partOfDay.HasValue)
        {
            return relativePath;
        }

        // Highlight the file name within its daypart folder for clarity.
        var fileName = Path.GetFileName(relativePath);
        return string.Concat(partOfDay.Value, ": ", fileName);
    }

    private static PartOfDayEnum? ResolvePartOfDay(string relativePath)
    {
        var firstSegment = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(firstSegment))
        {
            return null;
        }

        return _partOfDayFolderMap.TryGetValue(firstSegment, out var partOfDay)
            ? partOfDay
            : null;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static IEnumerable<string> EnumerateLevelFiles(string levelsDirectory, string extension)
    {
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? ".json"
            : "." + extension.TrimStart('.');

        var assetDirectoryPath = TryToAssetPath(levelsDirectory);
        if (!string.IsNullOrEmpty(assetDirectoryPath) && AssetDatabase.IsValidFolder(assetDirectoryPath))
        {
            return AssetDatabase
                .FindAssets(string.Empty, new[] { assetDirectoryPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => assetPath.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase))
                .Select(ToAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return Directory.GetFiles(levelsDirectory, $"*{normalizedExtension}", SearchOption.AllDirectories);
    }

    [Obsolete("Use GetLevelFileDescriptors for both locations and level design templates.")]
    public static List<string> GetLevelFiles(string levelsDirectory, string extension = "json")
    {
        if (!Directory.Exists(levelsDirectory))
        {
            Debug.LogError($"Directory does not exist: {levelsDirectory}");
            return new List<string>();
        }

        return Directory.GetFiles(levelsDirectory, $"*.{extension}")
                        .ToList();
    }

    public static LevelInfo LoadLevel(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var levelInfo = JsonUtility.FromJson<LevelInfo>(json);

            var errors = ValidateLevelInfo(levelInfo);

            if (errors.Any())
            {
                throw new Exception($"Level data is invalid: {string.Join(", ", errors)}");
            }

            return levelInfo;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load level from {filePath}: {ex.Message}");
            return null;
        }
    }

    public static void SaveLevel(LevelInfo levelInfo, string filePath)
    {
        try
        {
            var json = JsonUtility.ToJson(levelInfo, true);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save level to {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads PatternsCollection.json from the level_design_templates folder.
    /// </summary>
    public static PatternsCollection LoadPatternsCollection()
    {
        var path = Path.Combine(Consts.LocationsPath,
            Consts.TemplatesLocationName, "levels", "PatternsCollection.json");
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonUtility.FromJson<PatternsCollection>(json);
    }

    /// <summary>
    /// Loads LocationTheme (obstacle_sprite_to_type_mappings.json) for the given location folder.
    /// </summary>
    public static LocationTheme LoadLocationTheme(string locationFolder)
    {
        var path = Path.Combine(Consts.LocationsPath,
            locationFolder, "obstacle_sprite_to_type_mappings.json");
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonUtility.FromJson<LocationTheme>(json);
    }

    /// <summary>
    /// Loads a level JSON in the new reference-based format.
    /// </summary>
    public static LevelInfoRef LoadLevelRef(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Debug.LogWarning($"Level json file not found: {filePath}");
            return null;
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonUtility.FromJson<LevelInfoRef>(json);
    }

    /// <summary>
    /// Saves a level JSON in the new reference-based format.
    /// </summary>
    public static void SaveLevelRef(LevelInfoRef levelRef, string filePath)
    {
        var json = JsonUtility.ToJson(levelRef, true);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public static string CreateNewLevel(LevelInfo levelInfo, string levelsDirectory, PartOfDayEnum partOfDay, string requestedLevelName = null, List<string> spritesNames = null)
    {
        try
        {
            var errors = ValidateLevelInfo(levelInfo, spritesNames);

            if (errors.Any())
                throw new Exception($"Level data is invalid: {string.Join(", ", errors)}");

            var levelKey = ResolveLevelKey(levelsDirectory, requestedLevelName);
            var filePath = BuildCanonicalLevelJsonPath(levelsDirectory, partOfDay, levelKey);

            SaveLevel(levelInfo, filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create new level: {ex.Message}");
            return null;
        }
    }

    public static string CreateNewLevelRef(LevelInfoRef levelInfoRef, string levelsDirectory, PartOfDayEnum partOfDay, string requestedLevelName = null, List<string> spritesNames = null)
    {
        try
        {
            var errors = ValidateLevelInfo(new LevelInfo
            {
                skyTexture = levelInfoRef?.skyTexture,
                roadTexture = levelInfoRef?.roadTexture
            }, spritesNames);

            if (errors.Any())
                throw new Exception($"Level data is invalid: {string.Join(", ", errors)}");

            var levelKey = ResolveLevelKey(levelsDirectory, requestedLevelName);
            var filePath = BuildCanonicalLevelJsonPath(levelsDirectory, partOfDay, levelKey);

            SaveLevelRef(levelInfoRef, filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create new level: {ex.Message}");
            return null;
        }
    }

    public static string NormalizeLevelKey(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return string.Empty;

        var normalized = levelName.Trim().ToLowerInvariant()
            .Replace('-', '_')
            .Replace(' ', '_');
        normalized = _levelKeySanitizer.Replace(normalized, "_");
        while (normalized.Contains("__", StringComparison.Ordinal))
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);

        return normalized.Trim('_');
    }

    public static string GetLevelKeyFromFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        var levelDirectory = Path.GetFileName(Path.GetDirectoryName(filePath));
        if (!string.IsNullOrWhiteSpace(levelDirectory))
            return levelDirectory;

        return Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
    }

    public static string GetNextAvailableLevelKey(string levelsDirectory)
    {
        return GenerateNextLevelKey(levelsDirectory);
    }

    private static string ResolveLevelKey(string levelsDirectory, string requestedLevelName, string currentLevelKeyToIgnore = null)
    {
        var normalizedRequested = NormalizeLevelKey(requestedLevelName);
        if (!string.IsNullOrWhiteSpace(normalizedRequested))
        {
            EnsureLevelKeyIsAvailable(levelsDirectory, normalizedRequested, currentLevelKeyToIgnore);
            return normalizedRequested;
        }

        return GenerateNextLevelKey(levelsDirectory);
    }

    private static string GenerateNextLevelKey(string levelsDirectory)
    {
        var highestLevelNumber = 0;

        foreach (var descriptor in EnumerateAllLocationLevelFileDescriptors())
        {
            var levelKey = GetLevelKeyFromFilePath(descriptor.AbsolutePath);
            var parts = levelKey.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out var levelNumber))
            {
                if (levelNumber > highestLevelNumber)
                {
                    highestLevelNumber = levelNumber;
                }
            }
        }

        return $"level_{highestLevelNumber + 1:D2}";
    }

    private static void EnsureLevelKeyIsAvailable(string levelsDirectory, string levelKey, string currentLevelKeyToIgnore)
    {
        foreach (var descriptor in EnumerateAllLocationLevelFileDescriptors())
        {
            var existingKey = GetLevelKeyFromFilePath(descriptor.AbsolutePath);
            if (string.Equals(existingKey, currentLevelKeyToIgnore, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(existingKey, levelKey, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Level '{levelKey}' already exists.");
        }
    }

    private static string BuildCanonicalLevelJsonPath(string levelsDirectory, PartOfDayEnum partOfDay, string levelKey)
    {
        var partDirectory = Path.Combine(levelsDirectory, partOfDay.ToString());
        var levelDirectory = Path.Combine(partDirectory, levelKey);
        Directory.CreateDirectory(levelDirectory);
        return Path.Combine(levelDirectory, $"{levelKey}.json");
    }

    private static string ToAssetPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return string.Empty;

        var normalizedAbsolutePath = Path.GetFullPath(absolutePath);
        var assetsRoot = Path.GetFullPath(Application.dataPath);

        if (string.Equals(normalizedAbsolutePath, assetsRoot, StringComparison.OrdinalIgnoreCase))
            return "Assets";

        var assetsRootWithSeparator = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!normalizedAbsolutePath.StartsWith(assetsRootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path '{absolutePath}' is outside Unity Assets root '{assetsRoot}'.");

        var relativePath = normalizedAbsolutePath.Substring(assetsRootWithSeparator.Length);
        return "Assets/" + NormalizePath(relativePath);
    }

    private static string TryToAssetPath(string absolutePath)
    {
        try
        {
            return ToAssetPath(absolutePath);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        var normalizedAssetPath = NormalizePath(assetPath);
        if (string.Equals(normalizedAssetPath, "Assets", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(Application.dataPath);

        if (!normalizedAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Asset path '{assetPath}' must start with 'Assets/'.");

        var assetsRootParent = Directory.GetParent(Path.GetFullPath(Application.dataPath));
        if (assetsRootParent == null)
            throw new InvalidOperationException("Failed to resolve Unity project root from Application.dataPath.");

        return Path.GetFullPath(Path.Combine(assetsRootParent.FullName, normalizedAssetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static string CreateNewTemplate(LevelInfo levelInfo, string templateName, string levelsDirectory,
        List<string> uiManagerInitialSprites)
    {
        try
        {
            var errors = ValidateLevelInfo(levelInfo, uiManagerInitialSprites);
            if (errors.Any())
                throw new Exception($"Level data is invalid: {string.Join(", ", errors)}");
  
            var newLevelFileName = $"template_{templateName}.json";
            var filePath = Path.Combine(levelsDirectory, newLevelFileName);
            SaveLevel(levelInfo, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create new template: {ex.Message}");
            return null;
        }
    }

    public static void SaveMappingsToAddressables(string location, Dictionary<string, ObstacleTypeEnum> spriteTypeBindings)
    {
    // Адрес, по которому будет сохраняться JSON-файл, который также зарегистрирован в Addressables
    var path = Path.Combine(Consts.LocationsPath, location, $"{MappingFileName}.json");   // Локальный путь в проекте Unity

        var mappings = new ObstacleSpriteTypeMappings
        {
            obstacle_sprite_to_type_mappings = spriteTypeBindings
                .GroupBy(entry => entry.Value)
                .Select(group => new ObstacleSpriteTypeMapping
                {
                    type = group.Key,
                    sprites = group.Select(entry => entry.Key).ToList()
                })
                .ToList()
        };

        var json = JsonUtility.ToJson(mappings, true);

        // Сохраняем JSON в локальный файл
        File.WriteAllText(path, json, Encoding.UTF8);

        // Импортируем файл в AssetDatabase, чтобы обновить его в Addressables
        AssetDatabase.ImportAsset(path);

        // ─── Addressables
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        var group = settings.FindGroup(MappingsGroupName) ??
                    settings.CreateGroup(MappingsGroupName, false, false, false, null);

        var entry = settings.CreateOrMoveEntry(
            AssetDatabase.AssetPathToGUID(path), group);

        entry.address = $"{location}/{MappingFileName}";


        // Обновляем настройки Addressables
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    public static void LoadMappingsFromAddressables(
        string location,
        Action<Dictionary<string, ObstacleTypeEnum>> callback)
    {
        if (string.IsNullOrEmpty(location)) 
        {
            callback?.Invoke(null);
            return;
        }

        var key = $"{location}/{MappingFileName}"; 

        /* ─── сначала смотрим, есть ли запись с таким ключом ─── */
        var locHandle = Addressables.LoadResourceLocationsAsync(key);
        locHandle.Completed += locOp =>
        {
            if (locOp.Result == null || locOp.Result.Count == 0)
            {
                // Файла нет — просто возвращаем null, без исключения
                callback?.Invoke(null);
                Addressables.Release(locHandle);
                return;
            }

            /* ─── Есть запись → грузим файл как раньше ─── */
            var assetHandle = Addressables.LoadAssetAsync<TextAsset>(key);
            assetHandle.Completed += h =>
            {
                var bindings = new Dictionary<string, ObstacleTypeEnum>();

                if (h.Status == AsyncOperationStatus.Succeeded)
                {
                    var mappings = JsonUtility.FromJson<ObstacleSpriteTypeMappings>(h.Result.text);
                    foreach (var m in mappings.obstacle_sprite_to_type_mappings)
                    foreach (var sprite in m.sprites)
                        bindings[Path.GetFileNameWithoutExtension(sprite)] = m.type;

                    callback?.Invoke(bindings);
                }
                else
                {
                    callback?.Invoke(null);
                }

                Addressables.Release(assetHandle);
                Addressables.Release(locHandle);
            };
        };
    }

    /// <summary>
    /// Загружает привязки типов препятствий из Addressables и исправляет типы препятствий в указанном файле
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="currentLevelInfo"></param>
    /// <param name="spriteTypeBindings"></param>
    public static void FixObstacleTypesInLevelInfoAndSaveToJson(string location, string filePath, LevelInfo currentLevelInfo)
    {
        if(string.IsNullOrEmpty(location))
        {
            Debug.LogError("Location is null or empty.");
            return;
        }

        var hasChanges = false;

        foreach (var pattern in currentLevelInfo.patterns)
        {
            foreach (var obstacle in pattern.obstacles)
            {
                var shortSpriteName = Path.GetFileNameWithoutExtension(obstacle.spriteName);

                if (ObstacleSpriteTypeMappingsManager.TryGetType(shortSpriteName, out var correctType))
                {
                    if (obstacle.type != (int)correctType)
                    {
                        obstacle.type = (int)correctType;
                        hasChanges = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"No mapping for sprite {obstacle.spriteName} in location '{location}'.");
                }
            }
        }

        if (hasChanges)
        {
            SaveLevel(currentLevelInfo, filePath);
        }
    }

    /// <summary>
    /// Валидация LevelInfo
    /// </summary>
    /// <param name="levelInfo"></param>
    /// <param name="spritesNames"></param>
    /// <returns></returns>
    public static List<string> ValidateLevelInfo(LevelInfo levelInfo, List<string> spritesNames = null)
    {
        var errors = new List<string>();
        if (levelInfo == null)
        {
            errors.Add("LevelInfo is null");
            return errors;
        }

        return errors;
    }

    [Obsolete("Use GetLevelFileDescriptors and filter as needed instead of scanning with this helper.")]
    public static List<string> GetLevelFilesFromAllLocations(string extension = "json")
    {
        var levelDirectories = Directory.GetDirectories(Consts.LocationsPath, "levels", SearchOption.AllDirectories);
        var levelFiles = new List<string>();

        foreach (var dir in levelDirectories)
        {
            levelFiles.AddRange(Directory.GetFiles(dir, $"*.{extension}"));
        }

        return levelFiles;
    }

    private static IEnumerable<LevelFileDescriptor> EnumerateAllLocationLevelFileDescriptors(string extension = "json")
    {
        if (!Directory.Exists(Consts.LocationsPath))
        {
            yield break;
        }

        var locationDirectories = Directory.GetDirectories(Consts.LocationsPath);
        foreach (var locationDirectory in locationDirectories)
        {
            var levelsDirectory = Path.Combine(locationDirectory, "levels");
            if (!Directory.Exists(levelsDirectory))
            {
                continue;
            }

            foreach (var descriptor in GetLevelFileDescriptors(levelsDirectory, extension))
            {
                yield return descriptor;
            }
        }
    }

}
