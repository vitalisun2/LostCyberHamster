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
                Debug.Log("[GameData] Load outcome: Primary.");

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

                Debug.LogWarning("[GameData] Load outcome: Backup promoted.");
                return Task.CompletedTask;
            }

            PlayerData = CreateDefaultPlayerData();
            EnsureProgressConsistency();
            EnsureValidated(PlayerData);
            PlayerPrefs.DeleteKey(_playerDataBackupKey);
            WritePrimary(PlayerData, rotateValidPrimary: false);
            Debug.LogWarning("[GameData] Load outcome: Defaults created.");

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

                if (validation.Status == PlayerDataValidationStatus.Valid)
                {
                    Debug.Log($"[GameData] Validation {GetSourceName(key)}: {(wasRepaired ? "Repaired" : "Valid")}.");
                }
                else
                {
                    Debug.LogWarning($"[GameData] Validation {GetSourceName(key)}: Rejected ({validation.Reason}).");
                }

                return validation.Status == PlayerDataValidationStatus.Valid;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameData] Load {GetSourceName(key)}: failed ({exception.GetType().Name}).");
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

        /// <summary>
        /// Проверяет и целиком заменяет локальные и текущие данные игрока.
        /// </summary>
        public static void ReplacePlayerData(PlayerData replacement)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            EnsureValidated(replacement);

            var previousPlayerData = PlayerData;
            var hadPrimary = PlayerPrefs.HasKey(_playerDataKey);
            var previousPrimary = PlayerPrefs.GetString(_playerDataKey, string.Empty);
            var hadBackup = PlayerPrefs.HasKey(_playerDataBackupKey);
            var previousBackup = PlayerPrefs.GetString(_playerDataBackupKey, string.Empty);

            try
            {
                PlayerPrefs.DeleteKey(_playerDataBackupKey);
                WritePrimary(replacement, rotateValidPrimary: false);
                PlayerData = replacement;
                Debug.Log("[GameData] ReplacePlayerData: success.");
            }
            catch
            {
                PlayerData = previousPlayerData;
                RestorePlayerPrefsValue(_playerDataKey, hadPrimary, previousPrimary);
                RestorePlayerPrefsValue(_playerDataBackupKey, hadBackup, previousBackup);

                try
                {
                    PlayerPrefs.Save();
                }
                catch (Exception rollbackException)
                {
                    Debug.LogError($"[GameData] ReplacePlayerData rollback failed ({rollbackException.GetType().Name}).");
                }

                throw;
            }
        }

        private static void RestorePlayerPrefsValue(string key, bool existed, string value)
        {
            if (existed)
                PlayerPrefs.SetString(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }

        private static void WritePrimary(PlayerData data, bool rotateValidPrimary)
        {
            bool backupRotated = false;
            if (rotateValidPrimary && TryGetStrictlyValidEncryptedData(_playerDataKey, out var currentPrimary))
            {
                PlayerPrefs.SetString(_playerDataBackupKey, currentPrimary);
                backupRotated = true;
            }

            try
            {
                data.LastSaveDate = DateTime.UtcNow.ToString("o");
                var serializedData = data.ToJson();
                var encryptedData = _cryptoService.Encrypt(serializedData);
                PlayerPrefs.SetString(_playerDataKey, encryptedData);
                PlayerPrefs.Save();
                Debug.Log($"[GameData] Save: primary written; backup {(backupRotated ? "rotated" : "skipped")}.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameData] Save: failed ({exception.GetType().Name}).");
                throw;
            }
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
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameData] Inspect {GetSourceName(key)}: failed ({exception.GetType().Name}).");
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
                Debug.LogWarning("[GameData] Recovery: rejected backup cleared.");
            }
        }

        private static string GetSourceName(string key)
        {
            return string.Equals(key, _playerDataBackupKey, StringComparison.Ordinal)
                ? "Backup"
                : "Primary";
        }

        private static void EnsureValidated(PlayerData data)
        {
            var validation = PlayerDataValidator.Validate(data);
            if (validation.Status == PlayerDataValidationStatus.Repairable)
            {
                PlayerDataValidator.RepairSafe(data, validation);
                validation = PlayerDataValidator.Validate(data);
                Debug.Log("[GameData] Validation runtime: Repaired.");
            }

            if (validation.Status != PlayerDataValidationStatus.Valid)
            {
                Debug.LogWarning($"[GameData] Validation runtime: Rejected ({validation.Reason}).");
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
            var defaultData = CreateDefaultPlayerData();

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
            Debug.Log("[GameData] ResetPlayerProgress: success.");
        }

        public static void ResetSettings()
        {
            PlayerPrefs.DeleteKey(_settingsKey);
            Settings = new SettingsData();
            SaveSettings();
            Debug.Log("[GameData] ResetSettings: success.");
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

        /// <summary>
        /// Создаёт данные нового игрока с полным адресом первого уровня настроенного каталога.
        /// </summary>
        private static PlayerData CreateDefaultPlayerData()
        {
            // Требуем готовый непустой каталог до создания сохраняемых данных игрока.
            var catalog = LevelCatalogService.Catalog;
            if (catalog.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Cannot create default player data: level catalog is not configured or empty.");
            }

            // Берём первый уровень в иерархическом порядке и сохраняем его полный address.
            var firstLevel = catalog.EnumerateLevels()
                .OrderBy(level => level.LocationIndex)
                .ThenBy(level => level.PartIndex)
                .ThenBy(level => level.LevelIndex)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(firstLevel.Address))
            {
                throw new InvalidOperationException(
                    "Cannot create default player data: first catalog level has no address.");
            }

            return new PlayerData
            {
                CurrentLevel = firstLevel.Address.Trim()
            };
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
