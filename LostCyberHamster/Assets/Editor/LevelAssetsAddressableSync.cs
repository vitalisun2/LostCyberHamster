#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Compilation;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Keeps Addressables entries for level-related assets in sync with Content/locations.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelAssetsAddressableSync
    {
        private const string LocationsRoot = "Assets/Content/locations";
        private const string LevelsByDayPartGroupName = "levels_by_daypart";
        private const string LegacyLevelsGroupName = "levels";
        private const string GlobalDayPartLabel = "levels_daypart";
        private const string LegacyLabel = "levels";

        static LevelAssetsAddressableSync()
        {
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.delayCall += () => SyncInternal(autoSave: false, logSilently: true);
        }

        private static void OnCompilationFinished(object obj)
        {
            EditorApplication.delayCall += () => SyncInternal(autoSave: false, logSilently: true);
        }

        [MenuItem("Tools/Levels/Sync Level Assets", priority = 500)]
        public static void SyncFromMenu()
        {
            SyncInternal(autoSave: true, logSilently: false);
        }

        private static void SyncInternal(bool autoSave, bool logSilently)
        {
            if (Application.isPlaying)
            {
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (!logSilently)
                {
                    Debug.LogWarning("[LevelAssetsAddressableSync] AddressableAssetSettings not found.");
                }
                return;
            }

            var levelsByDayPartGroup = settings.FindGroup(LevelsByDayPartGroupName);
            if (levelsByDayPartGroup == null)
            {
                if (!logSilently)
                {
                    Debug.LogWarning($"[LevelAssetsAddressableSync] Group '{LevelsByDayPartGroupName}' is missing.");
                }
                return;
            }

            var legacyLevelsGroup = settings.FindGroup(LegacyLevelsGroupName);
            if (legacyLevelsGroup == null)
            {
                if (!logSilently)
                {
                    Debug.LogWarning($"[LevelAssetsAddressableSync] Group '{LegacyLevelsGroupName}' is missing.");
                }
                return;
            }

            if (!Directory.Exists(LocationsRoot))
            {
                if (!logSilently)
                {
                    Debug.LogWarning($"[LevelAssetsAddressableSync] Locations directory not found: {LocationsRoot}");
                }
                return;
            }

            var changed = false;

            foreach (var locationDir in Directory.GetDirectories(LocationsRoot))
            {
                var locationKey = Path.GetFileName(locationDir);
                if (string.IsNullOrEmpty(locationKey))
                {
                    continue;
                }

                var levelsDir = Path.Combine(locationDir, "levels");
                if (!Directory.Exists(levelsDir))
                {
                    continue;
                }

                changed |= SyncLegacyLevels(settings, legacyLevelsGroup, levelsDir);
                changed |= SyncLevelsByDayPart(settings, levelsByDayPartGroup, levelsDir, locationKey);
            }

            var validationErrors = ValidateLevelAssets();
            if (validationErrors.Count > 0)
            {
                foreach (var error in validationErrors.Distinct())
                {
                    Debug.LogWarning($"[LevelAssetsAddressableSync] {error}");
                }
            }

            if (changed)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
                if (autoSave)
                {
                    AssetDatabase.SaveAssets();
                }

                if (!logSilently)
                {
                }
            }
            else if (!logSilently)
            {
            }
        }

        private static bool SyncLegacyLevels(AddressableAssetSettings settings, AddressableAssetGroup legacyLevelsGroup, string levelsDir)
        {
            var changed = false;
            foreach (var file in Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                changed |= EnsureLegacyEntry(settings, legacyLevelsGroup, file);
            }

            return changed;
        }

        private static bool SyncLevelsByDayPart(AddressableAssetSettings settings, AddressableAssetGroup levelsByDayPartGroup, string levelsDir, string locationKey)
        {
            var changed = false;
            foreach (var partDir in Directory.GetDirectories(levelsDir))
            {
                var partKey = Path.GetFileName(partDir);
                if (string.IsNullOrEmpty(partKey))
                {
                    continue;
                }

                foreach (var levelDir in Directory.GetDirectories(partDir))
                {
                    var levelKey = Path.GetFileName(levelDir);
                    if (string.IsNullOrEmpty(levelKey))
                    {
                        continue;
                    }

                    var levelAssetPath = EnsureLevelJsonAsset(levelDir, levelKey);
                    if (!string.IsNullOrEmpty(levelAssetPath))
                    {
                        changed |= EnsureLevelEntry(settings, levelsByDayPartGroup, levelAssetPath, locationKey, partKey, levelKey);
                    }

                    changed |= SyncLevelIntroSprites(settings, levelsByDayPartGroup, levelDir, locationKey, partKey, levelKey);
                }

                foreach (var file in Directory.GetFiles(partDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var levelKey = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(levelKey))
                    {
                        continue;
                    }

                    var levelDir = Path.Combine(partDir, levelKey);
                    if (!Directory.Exists(levelDir))
                    {
                        var parentAssetPath = ToAssetPath(partDir);
                        if (!string.IsNullOrEmpty(parentAssetPath))
                        {
                            AssetDatabase.CreateFolder(parentAssetPath, levelKey);
                        }
                    }

                    var canonicalPath = EnsureLevelJsonAsset(levelDir, levelKey);
                    if (!string.IsNullOrEmpty(canonicalPath))
                    {
                        changed |= EnsureLevelEntry(settings, levelsByDayPartGroup, canonicalPath, locationKey, partKey, levelKey);
                    }
                }
            }

            return changed;
        }

        private static bool EnsureLegacyEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string filePath)
        {
            var assetPath = ToAssetPath(filePath);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry == null)
                {
                    return false;
                }
            }
            else if (entry.parentGroup != group)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
            }

            var expectedAddress = Path.GetFileNameWithoutExtension(assetPath);
            var changed = UpdateAddress(entry, expectedAddress);
            changed |= EnsureLabel(settings, entry, LegacyLabel);
            return changed;
        }

        private static bool EnsureLevelEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string filePath, string locationKey, string partKey, string levelKey)
        {
            if (string.IsNullOrWhiteSpace(levelKey))
            {
                levelKey = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            }

            var address = BuildLevelAddress(locationKey, partKey, levelKey);
            return EnsureLevelAssetEntry(
                settings,
                group,
                filePath,
                address,
                locationKey,
                partKey,
                HierarchicalLevelCatalog.IsGameplayLevelKey(levelKey));
        }

        private static bool EnsureLevelAssetEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string filePath, string address, string locationKey, string partKey, bool includeGameplayLabels)
        {
            var assetPath = ToAssetPath(filePath);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry == null)
                {
                    return false;
                }
            }
            else if (entry.parentGroup != group)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
            }

            var changed = UpdateAddress(entry, address);
            changed |= SetGameplayLabels(settings, entry, locationKey, partKey, includeGameplayLabels);
            return changed;
        }

        private static bool SyncLevelIntroSprites(AddressableAssetSettings settings, AddressableAssetGroup group, string levelDir, string locationKey, string partKey, string levelKey)
        {
            if (!Directory.Exists(levelDir))
            {
                return false;
            }

            var changed = false;
            foreach (var introFile in Directory.GetFiles(levelDir, "intro_*.png", SearchOption.TopDirectoryOnly))
            {
                var introName = Path.GetFileNameWithoutExtension(introFile);
                if (string.IsNullOrEmpty(introName))
                {
                    continue;
                }

                var address = BuildLevelAddress(locationKey, partKey, levelKey) + "/" + introName;
                changed |= EnsureLevelAssetEntry(
                    settings,
                    group,
                    introFile,
                    address,
                    locationKey,
                    partKey,
                    includeGameplayLabels: false);
            }

            return changed;
        }

        private static List<string> ValidateLevelAssets()
        {
            var errors = new List<string>();

            if (!Directory.Exists(LocationsRoot))
            {
                return errors;
            }

            foreach (var locationDir in Directory.GetDirectories(LocationsRoot))
            {
                var locationKey = Path.GetFileName(locationDir);
                if (string.IsNullOrEmpty(locationKey))
                {
                    continue;
                }

                var levelsDir = Path.Combine(locationDir, "levels");
                if (!Directory.Exists(levelsDir))
                {
                    continue;
                }

                foreach (var partDir in Directory.GetDirectories(levelsDir))
                {
                    var partKey = Path.GetFileName(partDir);
                    if (string.IsNullOrEmpty(partKey))
                    {
                        continue;
                    }

                    foreach (var levelDir in Directory.GetDirectories(partDir))
                    {
                        var levelKey = Path.GetFileName(levelDir);
                        if (string.IsNullOrEmpty(levelKey))
                        {
                            continue;
                        }

                        var introFiles = Directory.GetFiles(levelDir, "intro_*.png", SearchOption.TopDirectoryOnly);
                        if (introFiles.Length == 0)
                        {
                            var address = BuildLevelAddress(locationKey, partKey, levelKey);
                            errors.Add($"Level '{address}' has no intro sprites.");
                        }
                    }
                }
            }

            return errors;
        }

        private static string BuildLevelAddress(string locationKey, string partKey, string levelKey)
        {
            return $"{locationKey}/{partKey}/{levelKey}";
        }

        private static string EnsureLevelJsonAsset(string levelDir, string levelKey)
        {
            if (string.IsNullOrEmpty(levelDir))
            {
                return string.Empty;
            }

            var expectedPath = Path.Combine(levelDir, levelKey + ".json");
            var expectedAssetPath = ToAssetPath(expectedPath);

            if (File.Exists(expectedPath))
            {
                return expectedAssetPath;
            }

            if (!Directory.Exists(levelDir))
            {
                Directory.CreateDirectory(levelDir);
            }

            var jsonFiles = Directory.GetFiles(levelDir, "*.json", SearchOption.TopDirectoryOnly);
            if (jsonFiles.Length == 0)
            {
                return expectedAssetPath;
            }

            var currentPath = jsonFiles[0];
            var currentAssetPath = ToAssetPath(currentPath);
            if (string.Equals(currentAssetPath, expectedAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return expectedAssetPath;
            }

            var moveResult = AssetDatabase.MoveAsset(currentAssetPath, expectedAssetPath);
            if (!string.IsNullOrEmpty(moveResult))
            {
                Debug.LogWarning($"[LevelAssetsAddressableSync] Failed to rename level asset '{currentAssetPath}' to '{expectedAssetPath}': {moveResult}");
                return currentAssetPath;
            }

            return expectedAssetPath;
        }

        private static bool UpdateAddress(AddressableAssetEntry entry, string expectedAddress)
        {
            if (!string.Equals(entry.address, expectedAddress, StringComparison.Ordinal))
            {
                entry.SetAddress(expectedAddress);
                return true;
            }

            return false;
        }

        private static bool EnsureLabel(AddressableAssetSettings settings, AddressableAssetEntry entry, string label)
        {
            return SetLabelState(settings, entry, label, enabled: true);
        }

        private static bool SetGameplayLabels(AddressableAssetSettings settings, AddressableAssetEntry entry, string locationKey, string partKey, bool enabled)
        {
            var changed = SetLabelState(settings, entry, GlobalDayPartLabel, enabled);
            changed |= SetLabelState(settings, entry, $"{GlobalDayPartLabel}_{partKey}", enabled);
            changed |= SetLabelState(settings, entry, $"levels_location_{locationKey}", enabled);
            return changed;
        }

        private static bool SetLabelState(AddressableAssetSettings settings, AddressableAssetEntry entry, string label, bool enabled)
        {
            if (string.IsNullOrEmpty(label))
            {
                return false;
            }

            if (enabled && !settings.GetLabels().Contains(label))
            {
                settings.AddLabel(label);
            }

            var hasLabel = entry.labels.Contains(label);
            if (hasLabel == enabled)
            {
                return false;
            }

            entry.SetLabel(label, enabled, true);
            return true;
        }

        private static string ToAssetPath(string filePath)
        {
            return filePath.Replace("\\", "/");
        }

        private static bool LooksLikeLevelAsset(string assetPath)
        {
            if (!assetPath.StartsWith(LocationsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && assetPath.Contains("/levels/", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(assetPath);
                return fileName != null && fileName.StartsWith("intro_", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        private sealed class LevelAssetPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (HasRelevantChange(importedAssets) || HasRelevantChange(deletedAssets) || HasRelevantChange(movedAssets) || HasRelevantChange(movedFromAssetPaths))
                {
                    SyncInternal(autoSave: true, logSilently: true);
                }
            }

            private static bool HasRelevantChange(IEnumerable<string> paths)
            {
                if (paths == null)
                {
                    return false;
                }

                foreach (var path in paths)
                {
                    if (LooksLikeLevelAsset(path))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
#endif















