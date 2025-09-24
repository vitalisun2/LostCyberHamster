using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class ContentStructureMigrator
{
    private const string LocationsRoot = "Assets/Content/Locations";
    private static readonly string[] PartOfDayNames =
    {
        "Morning",
        "Afternoon",
        "Evening",
        "Night"
    };

    [MenuItem("Tools/Content/Migrate To Part-Of-Day Structure", false, 1000)]
    public static void MigrateToPartOfDayStructure()
    {
        if (!Directory.Exists(LocationsRoot))
        {
            EditorUtility.DisplayDialog(
                "Content Structure Migrator",
                "Locations root not found at " + LocationsRoot,
                "OK");
            return;
        }

        var processedFiles = 0;
        var processedLocations = 0;
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var locationDirectory in Directory.GetDirectories(LocationsRoot))
            {
                var levelsPath = Path.Combine(locationDirectory, "Levels");
                if (!Directory.Exists(levelsPath))
                {
                    continue;
                }

                var levelFiles = Directory.GetFiles(levelsPath, "level_*.json", SearchOption.TopDirectoryOnly);
                if (levelFiles.Length == 0)
                {
                    CleanUpLevelsFolderIfEmpty(levelsPath);
                    continue;
                }

                var movedAnyInLocation = false;
                foreach (var levelFile in levelFiles)
                {
                    if (!TryParseLevelIndex(levelFile, out var levelIndex))
                    {
                        continue;
                    }

                    var partOfDay = GetPartOfDay(levelIndex);
                    var newFolderPath = EnsurePartOfDayFolder(locationDirectory, partOfDay);
                    var assetPath = NormalizeAssetPath(levelFile);
                    var newAssetPath = Path.Combine(newFolderPath, Path.GetFileName(levelFile)).Replace('\\', '/');

                    if (AssetDatabase.MoveAsset(assetPath, newAssetPath) == string.Empty)
                    {
                        Undo.IncrementCurrentGroup();
                        Debug.LogFormat("Moved {0} -> {1}", assetPath, newAssetPath);
                        UpdateAddressableEntry(settings, newAssetPath, partOfDay);
                        processedFiles++;
                        movedAnyInLocation = true;
                    }
                }

                if (movedAnyInLocation)
                {
                    processedLocations++;
                }

                CleanUpLevelsFolderIfEmpty(levelsPath);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        EditorUtility.DisplayDialog(
            "Content Structure Migrator",
            $"Processed {processedFiles} files across {processedLocations} locations.",
            "OK");
    }

    private static bool TryParseLevelIndex(string filePath, out int index)
    {
        index = 0;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName == null)
        {
            return false;
        }

        if (!fileName.StartsWith("level_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numberPart = fileName.Substring("level_".Length);
        return int.TryParse(numberPart, out index);
    }

    private static string GetPartOfDay(int levelIndex)
    {
        var idx = (levelIndex - 1) % PartOfDayNames.Length;
        if (idx < 0)
        {
            idx += PartOfDayNames.Length;
        }

        return PartOfDayNames[idx];
    }

    private static string EnsurePartOfDayFolder(string locationDirectory, string partOfDay)
    {
        var locationAssetPath = NormalizeAssetPath(locationDirectory);
        var partOfDayFolderPath = Path.Combine(locationAssetPath, partOfDay).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(partOfDayFolderPath))
        {
            AssetDatabase.CreateFolder(locationAssetPath, partOfDay);
        }

        return partOfDayFolderPath;
    }

    private static void UpdateAddressableEntry(AddressableAssetSettings settings, string newAssetPath, string partOfDay)
    {
        if (settings == null)
        {
            return;
        }

        var guid = AssetDatabase.AssetPathToGUID(newAssetPath);
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        var entry = settings.FindAssetEntry(guid);
        if (entry == null)
        {
            return;
        }

        entry.address = newAssetPath.Substring("Assets/".Length);
        entry.SetLabel("PartOfDay_" + partOfDay, true, true);
    }

    private static void CleanUpLevelsFolderIfEmpty(string levelsDirectory)
    {
        var assetPath = NormalizeAssetPath(levelsDirectory);
        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        var hasFiles = Directory.GetFiles(levelsDirectory, "*", SearchOption.AllDirectories).Length > 0;
        var hasDirectories = Directory.GetDirectories(levelsDirectory).Length > 0;
        if (!hasFiles && !hasDirectories)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static string NormalizeAssetPath(string path)
    {
        var projectPath = Directory.GetCurrentDirectory().Replace('\\', '/');
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith(projectPath + "/", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(projectPath.Length + 1);
        }

        return normalized;
    }
}
