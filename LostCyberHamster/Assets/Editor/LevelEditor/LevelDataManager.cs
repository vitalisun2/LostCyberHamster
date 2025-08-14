using Assets.Scripts.Common.Models;
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

public static class LevelDataManager
{
    private const string MappingFileName   = "obstacle_sprite_to_type_mappings";
    private const string MappingsGroupName = "Mappings";

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

    public static string CreateNewLevel(LevelInfo levelInfo, string levelsDirectory, List<string> spritesNames = null)
    {
        try
        {
            var errors = ValidateLevelInfo(levelInfo, spritesNames);

            if (errors.Any())
                throw new Exception($"Level data is invalid: {string.Join(", ", errors)}");

            var existingFiles = GetLevelFilesFromAllLocations();

            var highestLevelNumber = 0;

            foreach (var file in existingFiles)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                var parts = fileNameWithoutExtension.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out var levelNumber))
                {
                    if (levelNumber > highestLevelNumber)
                    {
                        highestLevelNumber = levelNumber;
                    }
                }
            }

            var newLevelNumber = highestLevelNumber + 1;
            var newLevelFileName = $"level_{newLevelNumber:D2}.json";

            var filePath = Path.Combine(levelsDirectory, newLevelFileName);

            SaveLevel(levelInfo, filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create new level: {ex.Message}");
            return null;
        }
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
        var fileName = "obstacle_sprite_to_type_mappings";
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
                        Debug.Log($"Mismatch found for {obstacle.spriteName}. Expected type: {correctType}, found: {obstacle.type}. Updating...");
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
            Debug.Log($"Level data '{filePath}' updated with correct types for location '{location}'.");
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

        if (spritesNames != null && !spritesNames.Contains(levelInfo.backgroundTexture))
        {
            errors.Add("Background texture is not set");
        }

        return errors;
    }

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

}
