#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Automatically adds bg_2_new_york_* sprites to Addressables when detected.
    /// </summary>
    public static class Background2AddressableSetup
    {
        private const string BackgroundsPath = "Assets/Content/locations/01_New_York/sprites/backgrounds";
        private const string BackgroundsGroupName = "backgrounds";
        private const string NewYorkLabel = "New York backgrounds";

        [MenuItem("Tools/Backgrounds/Setup Background2 Addressables", priority = 100)]
        public static void SetupBackground2Addressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Background2Setup] AddressableAssetSettings not found.");
                return;
            }

            var backgroundsGroup = settings.FindGroup(BackgroundsGroupName);
            if (backgroundsGroup == null)
            {
                Debug.LogError($"[Background2Setup] Group '{BackgroundsGroupName}' not found. Creating...");
                backgroundsGroup = settings.CreateGroup(BackgroundsGroupName, false, false, true, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema), typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
            }

            if (!Directory.Exists(BackgroundsPath))
            {
                Debug.LogError($"[Background2Setup] Backgrounds path not found: {BackgroundsPath}");
                return;
            }

            var bg2Files = new[]
            {
                "bg_2_new_york_morning.png",
                "bg_2_new_york_afternoon.png",
                "bg_2_new_york_evening.png",
                "bg_2_new_york_night.png"
            };

            int addedCount = 0;
            int skippedCount = 0;

            foreach (var fileName in bg2Files)
            {
                var fullPath = Path.Combine(BackgroundsPath, fileName);
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"[Background2Setup] File not found: {fullPath}");
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(fullPath);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning($"[Background2Setup] Cannot get GUID for: {fullPath}");
                    continue;
                }

                // Check if already in Addressables
                var existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null)
                {
                    Debug.Log($"[Background2Setup] Already in Addressables: {fileName} (skipping)");
                    skippedCount++;
                    continue;
                }

                // Create new entry
                var entry = settings.CreateOrMoveEntry(guid, backgroundsGroup, false, false);
                if (entry == null)
                {
                    Debug.LogError($"[Background2Setup] Failed to create entry for: {fileName}");
                    continue;
                }

                // Set address (filename without extension)
                entry.address = Path.GetFileNameWithoutExtension(fileName);

                // Add label
                entry.SetLabel(NewYorkLabel, true, true, false);

                Debug.Log($"[Background2Setup] Added to Addressables: {fileName} with label '{NewYorkLabel}'");
                addedCount++;
            }

            if (addedCount > 0 || skippedCount > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Background2Setup] Complete. Added: {addedCount}, Skipped: {skippedCount}");
            }
            else
            {
                Debug.LogWarning("[Background2Setup] No files were processed.");
            }
        }
    }
}
#endif
