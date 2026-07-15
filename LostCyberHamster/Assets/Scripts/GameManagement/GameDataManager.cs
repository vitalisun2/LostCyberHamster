using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement.Progress;
using Unity.Services.CloudSave;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement
{
    public static class GameDataManager
    {
        public static PlayerData PlayerData = new PlayerData();
        public static SettingsData Settings = new SettingsData();

        private static readonly string _playerDataKey = "PlayerData";
        private static readonly string _settingsKey = "Settings";

        private static readonly ICryptoService _cryptoService = new AesCryptoService();

    public static bool IsGameJustStarted = true;

        public static async Task LoadDataAsync()
        {
            var localData = LoadFromPlayerPrefs();
            if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
            {
                PlayerData = localData;
                SaveData();
                EnsureProgressConsistency();
                DebugManager.DiagStability("[AUTOMATION] Cloud Save load skipped for test-level run.");
                return;
            }

            var cloudData = await LoadFromCloud();

            DateTime.TryParse(localData.LastSaveDate, out var localLastSaveDate);
            DateTime.TryParse(cloudData.LastSaveDate, out var cloudLastSaveDate);

            PlayerData = cloudLastSaveDate > localLastSaveDate ? cloudData : localData;

            SaveData();

            EnsureProgressConsistency();

       }

        private static PlayerData LoadFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(_playerDataKey))
            {
               return new PlayerData();
            }

            var encryptedData = PlayerPrefs.GetString(_playerDataKey);
            var decryptedData = _cryptoService.Decrypt(encryptedData);
            return PlayerData.FromJson(decryptedData);
        }

        private static async Task<PlayerData> LoadFromCloud()
        {
            try
            {
                var keys = new HashSet<string> { _playerDataKey };
                var cloudData = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (cloudData.TryGetValue(_playerDataKey, out var cloudJson))
                {
                    return PlayerData.FromJson(cloudJson.Value.GetAs<string>());
                }

           }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to load data from Cloud: " + e.Message);
            }

            return new PlayerData();
        }

        public static void SaveData()
        {
            PlayerData.LastSaveDate = DateTime.UtcNow.ToString("o");
            var serializedData = PlayerData.ToJson();
           var encryptedData = _cryptoService.Encrypt(serializedData);
            PlayerPrefs.SetString(_playerDataKey, encryptedData);
            PlayerPrefs.Save();
        }

        public static void PurchaseSkin(int skinId)
        {
            if (!PlayerData.PurchasedSkinIds.Contains(skinId))
            {
                PlayerData.PurchasedSkinIds.Add(skinId);
                SaveData();
                TrySaveToCloud();
            }
        }

        private static async void TrySaveToCloud()
        {
            SaveData();
            if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
            {
                DebugManager.DiagStability("[AUTOMATION] Cloud Save save skipped for test-level run.");
                return;
            }

            var playerDataDict = new Dictionary<string, object>
            {
                { _playerDataKey, PlayerData.ToJson() }
            };

            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(playerDataDict);
           }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to save data to Cloud: " + e.Message);
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

            EnsureProgressConsistency();
        }

        public static void ClearData()
        {
            PlayerPrefs.DeleteAll();
        }

        /// <summary>
        /// Сбрасывает локальный прогресс и восстанавливает runtime-хранилища в начальное состояние.
        /// </summary>
        public static void ResetLocalData()
        {
            ClearData();
            PlayerPrefs.Save();

            PlayerData = new PlayerData();
            Settings = new SettingsData();
            IsGameJustStarted = true;

            EnsureProgressConsistency();
            SaveData();
            SaveSettings();

            MoneyStorage.Init(0);
            CrystalStorage.Init(0);
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
