using System;
using System.IO;
using GameManagement;
using UnityEditor;
using UnityEngine;
using Vues.GameCore;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Minimal menu entries to export or reset local player data.
    /// </summary>
    public static class GameDataDebugMenu
    {
        private const string PlayerDataKey = "PlayerData";
        private static readonly ICryptoService CryptoService = new AesCryptoService();

        [MenuItem("Tools/Player Data/Export JSON", priority = 100)]
        private static void ExportPlayerData()
        {
            try
            {
                if (!PlayerPrefs.HasKey(PlayerDataKey))
                {
                    Debug.LogWarning($"[Player Data] PlayerPrefs key '{PlayerDataKey}' not found.");
                    return;
                }

                var encrypted = PlayerPrefs.GetString(PlayerDataKey);
                if (string.IsNullOrWhiteSpace(encrypted))
                {
                    Debug.LogWarning($"[Player Data] PlayerPrefs key '{PlayerDataKey}' is empty.");
                    return;
                }

                var json = CryptoService.Decrypt(encrypted);
                var tempFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
                Directory.CreateDirectory(tempFolder);

                var outputPath = Path.Combine(tempFolder, "PlayerData.json");
                File.WriteAllText(outputPath, json);

            }
            catch (Exception ex)
            {
                Debug.LogError($"[Player Data] Export failed: {ex.Message}");
            }
        }

        [MenuItem("Tools/Player Data/Reset", priority = 101)]
        private static void ResetPlayerData()
        {
            try
            {
                GameDataManager.ClearData();
                PlayerPrefs.Save();

                GameDataManager.PlayerData = new PlayerData();
                GameDataManager.Settings = new SettingsData();
                GameDataManager.IsGameJustStarted = true;

                GameDataManager.SaveData();
                GameDataManager.SaveSettings();

                MoneyStorage.Init(0);
                CrystalStorage.Init(0);

                ExportPlayerData();

            }
            catch (Exception ex)
            {
                Debug.LogError($"[Player Data] Reset failed: {ex.Message}");
            }
        }
    }
}
