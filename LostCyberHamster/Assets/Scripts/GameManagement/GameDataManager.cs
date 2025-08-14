using System.Collections.Generic;
using Unity.Services.Core;
using Vues.GameCore;
using UnityEngine;
using System;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Threading.Tasks;

namespace GameManagement
{
    public static class GameDataManager
    {
        public static PlayerData PlayerData = new PlayerData();

        private static string _playerDataKey = "PlayerData";

        public static SettingsData Settings = new SettingsData();

        private static string _settingsKey = "Settings";

        /// <summary>
        /// Сервис шифрования данных.
        /// </summary>
        private static ICryptoService _cryptoService = new AesCryptoService();

        public static bool IsGameJustStarted = true;



        /// <summary>
        /// Инициализация менеджера данных.
        /// </summary>
        /// <param name="cryptoService"></param>
        public static async void InitializeAsync()
        {
            
        }

        public static async Task LoadDataAsync()
        {
            Debug.Log("Loading data...");
            PlayerData localData = LoadFromPlayerPrefs();
            PlayerData cloudData = await LoadFromCloud();

            DateTime.TryParse(localData.LastSaveDate, out DateTime localLastSaveDate);
            DateTime.TryParse(cloudData.LastSaveDate, out DateTime cloudLastSaveDate);
            if (cloudLastSaveDate > localLastSaveDate)
            {
                Debug.Log("Cloud data is newer.");
                PlayerData = cloudData;
                SaveData();
                return;
            }

            PlayerData = localData;

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
            PlayerData localData = PlayerData.FromJson(decryptedData);
            return localData;
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
                else
                {
                    Debug.Log("No cloud data found.");
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
            string serializedData = PlayerData.ToJson();
            Debug.Log(serializedData);
            var encryptedData = _cryptoService.Encrypt(serializedData);
            PlayerPrefs.SetString(_playerDataKey, encryptedData);
            PlayerPrefs.Save();
        }

        // Purchase a skin and save
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
                var settings = PlayerPrefs.GetString(_settingsKey);
                Settings = JsonUtility.FromJson<SettingsData>(settings);
            }
        }

        public static void ClearData()
        {
            PlayerPrefs.DeleteAll();
        }
    }
}