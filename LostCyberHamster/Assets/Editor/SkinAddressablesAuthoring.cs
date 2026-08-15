#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Проверяет и регистрирует assets нового скина в штатных groups.
    /// </summary>
    internal static class SkinAddressablesAuthoring
    {
        /// <summary>
        /// Возвращает обязательные Addressables settings проекта.
        /// </summary>
        public static AddressableAssetSettings GetSettings()
        {
            return AddressableAssetSettingsDefaultObject.Settings ??
                   throw new InvalidOperationException(
                       "Addressables settings are missing.");
        }

        /// <summary>
        /// Проверяет наличие штатных skin groups.
        /// </summary>
        public static void ValidateSkinGroups(
            AddressableAssetSettings settings)
        {
            GetGroup(
                settings,
                SkinVisualContentLayout.VisualAddressablesGroup);
            GetGroup(
                settings,
                SkinVisualContentLayout.SkinSpritesAddressablesGroup);
        }

        /// <summary>
        /// Возвращает localization JSON assets из штатной group.
        /// </summary>
        public static IReadOnlyList<string> GetLocalizationPaths(
            AddressableAssetSettings settings)
        {
            AddressableAssetGroup group = GetGroup(
                settings,
                SkinVisualContentLayout.LocalizationAddressablesGroup);
            List<string> paths = group.entries
                .Where(entry => entry != null)
                .Select(entry => AssetDatabase.GUIDToAssetPath(entry.guid))
                .Where(path => string.Equals(
                    Path.GetExtension(path),
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (paths.Count == 0)
            {
                throw new InvalidOperationException(
                    "Addressables group 'Localization' has no JSON files.");
            }

            return paths;
        }

        /// <summary>
        /// Проверяет, что address ещё никому не принадлежит.
        /// </summary>
        public static void ValidateAddressAvailable(
            AddressableAssetSettings settings,
            string address)
        {
            AddressableAssetEntry conflict = settings.groups
                .Where(group => group != null)
                .SelectMany(group => group.entries)
                .Where(entry => entry != null)
                .FirstOrDefault(entry => string.Equals(
                    entry.address,
                    address,
                    StringComparison.Ordinal));
            if (conflict != null)
            {
                string path = AssetDatabase.GUIDToAssetPath(conflict.guid);
                throw new InvalidOperationException(
                    $"Addressables address '{address}' already belongs to " +
                    $"'{path}'.");
            }
        }

        /// <summary>
        /// Добавляет asset в указанную штатную group.
        /// </summary>
        public static void Register(
            AddressableAssetSettings settings,
            SkinAssetTransaction transaction,
            string groupName,
            string assetPath,
            string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new InvalidOperationException(
                    $"Addressable asset is missing: {assetPath}.");
            }

            ValidateAddressAvailable(settings, address);
            if (settings.FindAssetEntry(guid) != null)
            {
                throw new InvalidOperationException(
                    $"Asset already has an Addressables entry: {assetPath}.");
            }

            AddressableAssetGroup group = GetGroup(settings, groupName);
            transaction.TrackAddressableGuid(guid);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                guid,
                group,
                readOnly: false,
                postEvent: false);
            if (entry == null)
            {
                throw new InvalidOperationException(
                    $"Cannot register Addressable asset: {assetPath}.");
            }

            entry.SetAddress(address, postEvent: false);
        }

        /// <summary>
        /// Сохраняет добавленные entries и затронутые groups.
        /// </summary>
        public static void Save(
            AddressableAssetSettings settings)
        {
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                null,
                postEvent: true,
                settingsModified: true);
            AssetDatabase.SaveAssetIfDirty(GetGroup(
                settings,
                SkinVisualContentLayout.VisualAddressablesGroup));
            AssetDatabase.SaveAssetIfDirty(GetGroup(
                settings,
                SkinVisualContentLayout.SkinSpritesAddressablesGroup));
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static AddressableAssetGroup GetGroup(
            AddressableAssetSettings settings,
            string name)
        {
            return settings.FindGroup(name) ??
                   throw new InvalidOperationException(
                       $"Addressables group '{name}' is missing.");
        }
    }
}
#endif
