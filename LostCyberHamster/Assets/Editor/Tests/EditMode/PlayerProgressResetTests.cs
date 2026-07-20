using Assets.Scripts.System;
using GameManagement;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Tests.EditMode
{
    public sealed class PlayerProgressResetTests
    {
        private const string PlayerDataKey = "PlayerData";
        private const string PlayerDataBackupKey = "PlayerData.Backup";
        private const string SettingsKey = "Settings";
        private const string AccountKey = "Account.TestToken";
        private const string ForeignKey = "Foreign.TestValue";

        private PlayerData _previousPlayerData;
        private SettingsData _previousSettings;
        private HierarchicalLevelCatalog _previousCatalog;
        private readonly bool[] _savedKeyExists = new bool[5];
        private readonly string[] _savedValues = new string[5];

        [SetUp]
        public void SetUp()
        {
            _previousPlayerData = GameDataManager.PlayerData;
            _previousSettings = GameDataManager.Settings;
            _previousCatalog = LevelCatalogService.Catalog;
            Capture(0, PlayerDataKey);
            Capture(1, PlayerDataBackupKey);
            Capture(2, SettingsKey);
            Capture(3, AccountKey);
            Capture(4, ForeignKey);

            LevelCatalogService.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameDataManager.PlayerData = _previousPlayerData;
            GameDataManager.Settings = _previousSettings;
            LevelCatalogService.Configure(_previousCatalog);

            Restore(0, PlayerDataKey);
            Restore(1, PlayerDataBackupKey);
            Restore(2, SettingsKey);
            Restore(3, AccountKey);
            Restore(4, ForeignKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void ResetPlayerProgress_PreservesSettingsAndUnrelatedPreferences()
        {
            var settings = new SettingsData
            {
                Language = 7,
                MusicVolume = 0.25f,
                SfxVolume = 0.75f,
                EnableVibration = false
            };
            GameDataManager.Settings = settings;
            GameDataManager.SaveSettings();
            string settingsJson = PlayerPrefs.GetString(SettingsKey);

            PlayerPrefs.SetString(AccountKey, "account-value");
            PlayerPrefs.SetString(ForeignKey, "foreign-value");
            PlayerPrefs.SetString(PlayerDataBackupKey, "obsolete-backup");
            PlayerPrefs.Save();

            GameDataManager.PlayerData = new PlayerData { Money = 99 };
            GameDataManager.SaveData();

            GameDataManager.ResetPlayerProgress();

            Assert.AreSame(settings, GameDataManager.Settings);
            Assert.AreEqual(settingsJson, PlayerPrefs.GetString(SettingsKey));
            Assert.AreEqual("account-value", PlayerPrefs.GetString(AccountKey));
            Assert.AreEqual("foreign-value", PlayerPrefs.GetString(ForeignKey));
            Assert.IsFalse(PlayerPrefs.HasKey(PlayerDataBackupKey));
        }

        private void Capture(int index, string key)
        {
            _savedKeyExists[index] = PlayerPrefs.HasKey(key);
            _savedValues[index] = PlayerPrefs.GetString(key, string.Empty);
        }

        private void Restore(int index, string key)
        {
            if (_savedKeyExists[index])
            {
                PlayerPrefs.SetString(key, _savedValues[index]);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
