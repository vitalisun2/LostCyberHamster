#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Откатывает только assets и текстовые файлы текущей Add Skin операции.
    /// </summary>
    internal sealed class SkinAssetTransaction
    {
        private readonly AddressableAssetSettings _addressableSettings;
        private readonly List<string> _createdAssetRoots = new();
        private readonly List<string> _addressableGuids = new();
        private readonly Dictionary<string, byte[]> _textBackups =
            new(StringComparer.Ordinal);

        public SkinAssetTransaction(
            AddressableAssetSettings addressableSettings)
        {
            _addressableSettings = addressableSettings;
        }

        /// <summary>
        /// Запоминает новую корневую asset-папку для точечного удаления.
        /// </summary>
        public void TrackCreatedAssetRoot(string assetPath)
        {
            if (!_createdAssetRoots.Contains(assetPath))
                _createdAssetRoots.Add(assetPath);
        }

        /// <summary>
        /// Запоминает новую Addressables entry для точечного удаления.
        /// </summary>
        public void TrackAddressableGuid(string guid)
        {
            if (!string.IsNullOrWhiteSpace(guid) &&
                !_addressableGuids.Contains(guid))
            {
                _addressableGuids.Add(guid);
            }
        }

        /// <summary>
        /// Сохраняет исходный текст файла до первой записи.
        /// </summary>
        public void BackupTextAsset(string assetPath)
        {
            if (_textBackups.ContainsKey(assetPath))
                return;

            string physicalPath =
                FileUtil.GetPhysicalPath(assetPath);
            _textBackups.Add(
                assetPath,
                File.ReadAllBytes(physicalPath));
        }

        /// <summary>
        /// Восстанавливает catalog/localization, Addressables entries и новые папки.
        /// </summary>
        public string Rollback()
        {
            var errors = new List<string>();
            var modifiedGroups = new HashSet<AddressableAssetGroup>();

            // Удаляем только entries, созданные текущей операцией.
            foreach (string guid in _addressableGuids)
            {
                TryRollback(
                    () =>
                    {
                        AddressableAssetEntry entry =
                            _addressableSettings.FindAssetEntry(guid);
                        if (entry?.parentGroup != null)
                            modifiedGroups.Add(entry.parentGroup);
                        _addressableSettings.RemoveAssetEntry(
                            guid,
                            postEvent: false);
                    },
                    $"Addressables entry {guid}",
                    errors);
            }

            // Восстанавливаем текстовые sources of truth до удаления assets.
            foreach (KeyValuePair<string, byte[]> backup in _textBackups)
            {
                TryRollback(
                    () =>
                    {
                        string physicalPath =
                            FileUtil.GetPhysicalPath(backup.Key);
                        File.WriteAllBytes(physicalPath, backup.Value);
                        AssetDatabase.ImportAsset(
                            backup.Key,
                            ImportAssetOptions.ForceUpdate);
                    },
                    backup.Key,
                    errors);
            }

            // Удаляем новые roots в обратном порядке их создания.
            foreach (string assetPath in _createdAssetRoots.AsEnumerable()
                         .Reverse())
            {
                TryRollback(
                    () =>
                    {
                        if (AssetDatabase.IsValidFolder(assetPath) &&
                            !AssetDatabase.DeleteAsset(assetPath))
                        {
                            throw new InvalidOperationException(
                                "AssetDatabase.DeleteAsset returned false.");
                        }
                    },
                    assetPath,
                    errors);
            }

            TryRollback(
                () =>
                {
                    _addressableSettings.SetDirty(
                        AddressableAssetSettings.ModificationEvent.EntryRemoved,
                        null,
                        postEvent: true,
                        settingsModified: true);
                    foreach (AddressableAssetGroup group in modifiedGroups)
                        AssetDatabase.SaveAssetIfDirty(group);
                    AssetDatabase.SaveAssetIfDirty(_addressableSettings);
                    AssetDatabase.Refresh();
                },
                "final rollback save",
                errors);
            return string.Join(Environment.NewLine, errors);
        }

        private static void TryRollback(
            Action action,
            string label,
            ICollection<string> errors)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                errors.Add($"Rollback failed for {label}: {exception.Message}");
            }
        }
    }
}
#endif
