using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement.Progress;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement
{
    public static class GameDataManager
    {
        public static PlayerData PlayerData = new PlayerData();
        public static SettingsData Settings = new SettingsData();

        private static readonly string _playerDataKey = "PlayerData";
        private static readonly string _playerDataBackupKey = "PlayerData.Backup";
        private static readonly string _settingsKey = "Settings";

        private static readonly ICryptoService _cryptoService = new AesCryptoService();

        public static bool IsGameJustStarted = true;

        public static Task LoadDataAsync()
        {
            if (TryLoadValidated(_playerDataKey, out var primaryData, out var primaryWasRepaired, out var primaryJson))
            {
                PlayerData = primaryData;
                EnsureProgressConsistency();
                EnsureValidated(PlayerData);

                if (primaryWasRepaired || !string.Equals(primaryJson, PlayerData.ToJson(), StringComparison.Ordinal))
                {
                    WritePrimary(PlayerData, rotateValidPrimary: !primaryWasRepaired);
                }

                ClearInvalidBackup();

                return Task.CompletedTask;
            }

            if (TryLoadValidated(_playerDataBackupKey, out var backupData, out var backupWasRepaired, out _))
            {
                PlayerData = backupData;
                EnsureProgressConsistency();
                EnsureValidated(PlayerData);
                WritePrimary(PlayerData, rotateValidPrimary: false);
                if (backupWasRepaired)
                {
                    PlayerPrefs.SetString(_playerDataBackupKey, PlayerPrefs.GetString(_playerDataKey));
                    PlayerPrefs.Save();
                }

                return Task.CompletedTask;
            }

            PlayerData = new PlayerData();
            EnsureProgressConsistency();
            EnsureValidated(PlayerData);
            PlayerPrefs.DeleteKey(_playerDataBackupKey);
            WritePrimary(PlayerData, rotateValidPrimary: false);

            return Task.CompletedTask;
        }

        private static bool TryLoadValidated(
            string key,
            out PlayerData data,
            out bool wasRepaired,
            out string json)
        {
            data = null;
            wasRepaired = false;
            json = string.Empty;

            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            try
            {
                var encryptedData = PlayerPrefs.GetString(key);
                json = _cryptoService.Decrypt(encryptedData);
                data = PlayerData.FromJson(json);

                var validation = PlayerDataValidator.Validate(data);
                if (validation.Status == PlayerDataValidationStatus.Repairable)
                {
                    PlayerDataValidator.RepairSafe(data, validation);
                    validation = PlayerDataValidator.Validate(data);
                    wasRepaired = true;
                }

                return validation.Status == PlayerDataValidationStatus.Valid;
            }
            catch (Exception)
            {
                data = null;
                wasRepaired = false;
                json = string.Empty;
                return false;
            }
        }

        public static void SaveData()
        {
            EnsureValidated(PlayerData);
            WritePrimary(PlayerData, rotateValidPrimary: true);
        }

        private static void WritePrimary(PlayerData data, bool rotateValidPrimary)
        {
            if (rotateValidPrimary && TryGetStrictlyValidEncryptedData(_playerDataKey, out var currentPrimary))
            {
                PlayerPrefs.SetString(_playerDataBackupKey, currentPrimary);
            }

            data.LastSaveDate = DateTime.UtcNow.ToString("o");
            var serializedData = data.ToJson();
            var encryptedData = _cryptoService.Encrypt(serializedData);
            PlayerPrefs.SetString(_playerDataKey, encryptedData);
            PlayerPrefs.Save();
        }

        private static bool TryGetStrictlyValidEncryptedData(string key, out string encryptedData)
        {
            encryptedData = string.Empty;
            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            try
            {
                encryptedData = PlayerPrefs.GetString(key);
                var json = _cryptoService.Decrypt(encryptedData);
                var data = PlayerData.FromJson(json);
                return PlayerDataValidator.Validate(data).Status == PlayerDataValidationStatus.Valid;
            }
            catch (Exception)
            {
                encryptedData = string.Empty;
                return false;
            }
        }

        private static void ClearInvalidBackup()
        {
            if (PlayerPrefs.HasKey(_playerDataBackupKey) &&
                !TryGetStrictlyValidEncryptedData(_playerDataBackupKey, out _))
            {
                PlayerPrefs.DeleteKey(_playerDataBackupKey);
                PlayerPrefs.Save();
            }
        }

        private static void EnsureValidated(PlayerData data)
        {
            var validation = PlayerDataValidator.Validate(data);
            if (validation.Status == PlayerDataValidationStatus.Repairable)
            {
                PlayerDataValidator.RepairSafe(data, validation);
                validation = PlayerDataValidator.Validate(data);
            }

            if (validation.Status != PlayerDataValidationStatus.Valid)
            {
                throw new InvalidOperationException($"Player data rejected: {validation.Reason}");
            }
        }

        public static void SaveSettings()
        {
            var settings = JsonUtility.ToJson(Settings);
            PlayerPrefs.SetString(_settingsKey, settings);
            PlayerPrefs.Save();
        }

        public static void LoadSettings()
        {
            if (PlayerPrefs.HasKey(_settingsKey))
            {
                var settingsJson = PlayerPrefs.GetString(_settingsKey);
                Settings = JsonUtility.FromJson<SettingsData>(settingsJson);
            }
        }

        public static void ResetPlayerProgress()
        {
            var defaultData = new PlayerData();

            var validation = PlayerDataValidator.Validate(defaultData);
            if (validation.Status == PlayerDataValidationStatus.Repairable)
            {
                PlayerDataValidator.RepairSafe(defaultData, validation);
                validation = PlayerDataValidator.Validate(defaultData);
            }

            if (validation.Status == PlayerDataValidationStatus.Rejected)
            {
                throw new InvalidOperationException($"Default player data rejected: {validation.Reason}");
            }

            PlayerPrefs.DeleteKey(_playerDataKey);
            PlayerPrefs.DeleteKey(_playerDataBackupKey);
            PlayerData = defaultData;
            IsGameJustStarted = true;
            SaveData();
        }

        public static void ResetSettings()
        {
            PlayerPrefs.DeleteKey(_settingsKey);
            Settings = new SettingsData();
            SaveSettings();
        }

        private static void EnsureProgressConsistency()
        {
            if (!LevelCatalogService.HasCatalog)
            {
                return;
            }

            try
            {
                var catalog = LevelCatalogService.Catalog;
                if (catalog.IsEmpty)
                {
                    return;
                }

                var baseSnapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);
                var existingSnapshot = PlayerData.Progress;

                var existingEntries = new Dictionary<LevelProgressKey, LevelProgressEntry>();
                foreach (var entry in existingSnapshot.Entries)
                {
                    existingEntries[entry.Key] = entry;
                }

                var mergedEntries = new List<LevelProgressEntry>();
                foreach (var entry in baseSnapshot.Entries)
                {
                    if (existingEntries.TryGetValue(entry.Key, out var existing))
                    {
                        mergedEntries.Add(existing);
                    }
                    else
                    {
                        mergedEntries.Add(entry);
                    }
                }

                PlayerData.Progress = new LevelProgressSnapshot(mergedEntries);
                EnsureCurrentLevelValid(catalog);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameDataManager] Failed to align player progress with catalog: {ex.Message}");
            }
        }

        private static void EnsureCurrentLevelValid(HierarchicalLevelCatalog catalog)
        {
            if (catalog.IsEmpty)
            {
                return;
            }

            if (LevelCatalogService.TryFindLevel(PlayerData.CurrentLevel, out var descriptor))
            {
                if (!string.IsNullOrWhiteSpace(descriptor.Address))
                {
                    PlayerData.CurrentLevel = descriptor.Address.Trim();
                }

                return;
            }

            var firstLevel = catalog.EnumerateLevels()
                .OrderBy(level => level.LocationIndex)
                .ThenBy(level => level.PartIndex)
                .ThenBy(level => level.LevelIndex)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstLevel.Address))
            {
                PlayerData.CurrentLevel = firstLevel.Address.Trim();
            }
        }
    }
}
