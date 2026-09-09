using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>Регистрирует только два UI-ассета активностей через Unity API и обновляет generated проекты.</summary>
    public static class ReturnActivityAssetSetup
    {
        [MenuItem("Tools/Activities/Configure UI assets")]
        public static void Configure()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup("UI");
            if (group == null) throw new InvalidOperationException("UI Addressables group is missing.");

            // Unity создаёт metadata; стабильные адреса совпадают с ScreenEnum.
            foreach (string name in new[] { "ReturnActivitiesScreen", "ActivityRewardModal" })
            {
                string path = $"Assets/Content/ui/uxml/{name}.uxml";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException($"Asset import failed: {path}");
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = name;
            }
            EditorUtility.SetDirty(group);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, group, true);
            AssetDatabase.SaveAssets();

            // Тот же regeneration, что штатный bridge; сборка контента здесь не запускается.
            if (!TestLevelAutomationBridge.TryRegenerateProjectFiles(out string message))
                throw new InvalidOperationException(message);
            Debug.Log("[Activities] UI assets configured; " + message);
        }
    }
}
