#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Keeps Addressables entries for day-part level JSON files in sync with assets under Content/locations.
    /// </summary>
    [InitializeOnLoad]
    public static class DayPartLevelsAddressableSync
    {
        private const string LocationsRoot = "Assets/Content/locations";
        private const string DayPartGroupName = "levels_by_daypart";
        private const string LegacyGroupName = "levels";
        private const string GlobalDayPartLabel = "levels_daypart";
        private const string LegacyLabel = "levels";

        static DayPartLevelsAddressableSync()
        {
            EditorApplication.delayCall += () => SyncInternal(autoSave: false, logSilently: true);
        }

        [MenuItem("Tools/Levels/Sync Day-Part Addressables", priority = 500)]
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
                    Debug.LogWarning("[DayPartLevelsAddressableSync] AddressableAssetSettings not found.");
                }
                return;
            }

            var dayPartGroup = settings.FindGroup(DayPartGroupName);
            if (dayPartGroup == null)
            {
                if (!logSilently)
                {
                    Debug.LogWarning($"[DayPartLevelsAddressableSync] Group '{DayPartGroupName}' is missing.");
                }
                return;
            }

            var legacyGroup = settings.FindGroup(LegacyGroupName);
            if (legacyGroup == null)
            {
                if (!logSilently)
                {
                    Debug.LogWarning($"[DayPartLevelsAddressableSync] Group '{LegacyGroupName}' is missing.");
                }
                return;
            }

            if (!Directory.Exists(LocationsRoot))
            {
                if (!logSilently)
                {
                    Debug.LogWarning($"[DayPartLevelsAddressableSync] Locations directory not found: {LocationsRoot}");
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

                changed |= SyncLegacyLevels(settings, legacyGroup, levelsDir);
                changed |= SyncDayPartLevels(settings, dayPartGroup, levelsDir, locationKey);
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
                    Debug.Log("[DayPartLevelsAddressableSync] Addressables updated.");
                }
            }
            else if (!logSilently)
            {
                Debug.Log("[DayPartLevelsAddressableSync] Addressables already up to date.");
            }
        }

        private static bool SyncLegacyLevels(AddressableAssetSettings settings, AddressableAssetGroup legacyGroup, string levelsDir)
        {
            var changed = false;
            foreach (var file in Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                changed |= EnsureLegacyEntry(settings, legacyGroup, file);
            }

            return changed;
        }

        private static bool SyncDayPartLevels(AddressableAssetSettings settings, AddressableAssetGroup dayPartGroup, string levelsDir, string locationKey)
        {
            var changed = false;
            foreach (var partDir in Directory.GetDirectories(levelsDir))
            {
                var partKey = Path.GetFileName(partDir);
                if (string.IsNullOrEmpty(partKey))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(partDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    changed |= EnsureDayPartEntry(settings, dayPartGroup, file, locationKey, partKey);
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

        private static bool EnsureDayPartEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string filePath, string locationKey, string partKey)
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

            var address = $"{locationKey}/{partKey}/{Path.GetFileNameWithoutExtension(assetPath)}";
            var changed = UpdateAddress(entry, address);
            changed |= EnsureLabel(settings, entry, GlobalDayPartLabel);
            changed |= EnsureLabel(settings, entry, $"{GlobalDayPartLabel}_{partKey}");
            changed |= EnsureLabel(settings, entry, $"levels_location_{locationKey}");
            return changed;
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
            if (string.IsNullOrEmpty(label))
            {
                return false;
            }

            if (!settings.GetLabels().Contains(label))
            {
                settings.AddLabel(label);
            }

            if (!entry.labels.Contains(label))
            {
                entry.SetLabel(label, true, true);
                return true;
            }

            return false;
        }

        private static string ToAssetPath(string filePath)
        {
            return filePath.Replace("\\", "/");
        }

        private static bool LooksLikeLevelJson(string assetPath)
        {
            return assetPath.StartsWith(LocationsRoot, StringComparison.OrdinalIgnoreCase) && assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
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
                    if (LooksLikeLevelJson(path))
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
