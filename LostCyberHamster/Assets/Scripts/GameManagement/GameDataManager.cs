using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System.FeatureFlags;
using GameManagement.Progress;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
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

        public static async void InitializeAsync()
        {
        }

        public static void ApplyFeatureFlags()
        {
            DayPartLevelsFeature.InitializeFromSettings(Settings);
        }

        public static async Task LoadDataAsync()
        {
            Debug.Log("Loading data...");

            var localData = LoadFromPlayerPrefs();
            var cloudData = await LoadFromCloud();

            DateTime.TryParse(localData.LastSaveDate, out var localLastSaveDate);
            DateTime.TryParse(cloudData.LastSaveDate, out var cloudLastSaveDate);

            PlayerData = cloudLastSaveDate > localLastSaveDate ? cloudData : localData;

            PlayerProgressMigration.Initialize(PlayerData);

            SaveData();

            Debug.Log("Data loaded." + PlayerData.ToJson());
        }

        private static PlayerData LoadFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(_playerDataKey))
            {
                Debug.Log("No data found in PlayerPrefs.");
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

                Debug.Log("No cloud data found.");
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
            Debug.Log(serializedData);
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
            var playerDataDict = new Dictionary<string, object>
            {
                { _playerDataKey, PlayerData.ToJson() }
            };

            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(playerDataDict);
                Debug.Log("Data successfully saved to Cloud.");
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

            DayPartLevelsFeature.InitializeFromSettings(Settings);
        }

        public static void ClearData()
        {
            PlayerPrefs.DeleteAll();
        }
    }
}
