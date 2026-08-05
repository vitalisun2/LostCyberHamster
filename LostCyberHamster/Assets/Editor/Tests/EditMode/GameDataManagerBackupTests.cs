using System.Collections.Generic;
using Assets.Scripts.System;
using GameManagement;
using NUnit.Framework;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Tests.EditMode
{
    public sealed class GameDataManagerBackupTests
    {
        private const string PlayerDataKey = "PlayerData";
        private const string PlayerDataBackupKey = "PlayerData.Backup";

        private PlayerData _previousPlayerData;
        private HierarchicalLevelCatalog _previousCatalog;
        private bool _hadPrimary;
        private string _savedPrimary;
        private bool _hadBackup;
        private string _savedBackup;

        [SetUp]
        public void SetUp()
        {
            _previousPlayerData = GameDataManager.PlayerData;
            _previousCatalog = LevelCatalogService.Catalog;
            _hadPrimary = PlayerPrefs.HasKey(PlayerDataKey);
            _savedPrimary = PlayerPrefs.GetString(PlayerDataKey, string.Empty);
            _hadBackup = PlayerPrefs.HasKey(PlayerDataBackupKey);
            _savedBackup = PlayerPrefs.GetString(PlayerDataBackupKey, string.Empty);

            LevelCatalogService.Configure(PlayerProgressTestCatalog.Create());
            PlayerPrefs.DeleteKey(PlayerDataKey);
            PlayerPrefs.DeleteKey(PlayerDataBackupKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            GameDataManager.PlayerData = _previousPlayerData;
            LevelCatalogService.Configure(_previousCatalog);
            Restore(PlayerDataKey, _hadPrimary, _savedPrimary);
            Restore(PlayerDataBackupKey, _hadBackup, _savedBackup);
            PlayerPrefs.Save();
        }

        [Test]
        public void SaveData_SecondSaveBacksUpFirstValidPrimary()
        {
            GameDataManager.PlayerData = CreateValidData(11);
            GameDataManager.SaveData();
            string firstPrimary = PlayerPrefs.GetString(PlayerDataKey);

            GameDataManager.PlayerData.Money = 22;
            GameDataManager.SaveData();

            Assert.AreEqual(firstPrimary, PlayerPrefs.GetString(PlayerDataBackupKey));
            Assert.AreEqual(11, ReadPlayerData(PlayerDataBackupKey).Money);
            Assert.AreEqual(22, ReadPlayerData(PlayerDataKey).Money);
        }

        [Test]
        public void LoadDataAsync_CorruptPrimary_RestoresAndPromotesBackupWithoutRotatingCorruption()
        {
            GameDataManager.PlayerData = CreateValidData(11);
            GameDataManager.SaveData();
            GameDataManager.PlayerData.Money = 22;
            GameDataManager.SaveData();
            string validBackup = PlayerPrefs.GetString(PlayerDataBackupKey);

            PlayerPrefs.SetString(PlayerDataKey, "corrupt-primary");
            PlayerPrefs.Save();

            GameDataManager.LoadDataAsync().GetAwaiter().GetResult();

            Assert.AreEqual(11, GameDataManager.PlayerData.Money);
            Assert.AreEqual(11, ReadPlayerData(PlayerDataKey).Money);
            Assert.AreEqual(validBackup, PlayerPrefs.GetString(PlayerDataBackupKey));
        }

        [Test]
        public void SaveData_CorruptPrimary_DoesNotOverwriteValidBackup()
        {
            GameDataManager.PlayerData = CreateValidData(11);
            GameDataManager.SaveData();
            GameDataManager.PlayerData.Money = 22;
            GameDataManager.SaveData();
            string validBackup = PlayerPrefs.GetString(PlayerDataBackupKey);

            PlayerPrefs.SetString(PlayerDataKey, "corrupt-primary");
            GameDataManager.PlayerData = CreateValidData(33);

            GameDataManager.SaveData();

            Assert.AreEqual(validBackup, PlayerPrefs.GetString(PlayerDataBackupKey));
            Assert.AreEqual(33, ReadPlayerData(PlayerDataKey).Money);
        }

        [Test]
        public void LoadDataAsync_CorruptPrimaryAndBackup_FallsBackToValidatedDefault()
        {
            PlayerPrefs.SetString(PlayerDataKey, "corrupt-primary");
            PlayerPrefs.SetString(PlayerDataBackupKey, "corrupt-backup");
            PlayerPrefs.Save();

            GameDataManager.LoadDataAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, GameDataManager.PlayerData.Money);
            Assert.AreEqual(0, GameDataManager.PlayerData.Crystals);
            Assert.AreEqual(
                PlayerDataValidationStatus.Valid,
                PlayerDataValidator.Validate(GameDataManager.PlayerData).Status);
            Assert.AreEqual(
                PlayerDataValidationStatus.Valid,
                PlayerDataValidator.Validate(ReadPlayerData(PlayerDataKey)).Status);
            Assert.IsTrue(
                !PlayerPrefs.HasKey(PlayerDataBackupKey) ||
                PlayerDataValidator.Validate(ReadPlayerData(PlayerDataBackupKey)).Status ==
                PlayerDataValidationStatus.Valid);
        }

        [Test]
        public void LoadDataAsync_ValidPrimary_ClearsCorruptBackup()
        {
            GameDataManager.PlayerData = CreateValidData(17);
            GameDataManager.SaveData();
            PlayerPrefs.SetString(PlayerDataBackupKey, "corrupt-backup");
            PlayerPrefs.Save();

            GameDataManager.LoadDataAsync().GetAwaiter().GetResult();

            Assert.AreEqual(17, GameDataManager.PlayerData.Money);
            Assert.AreEqual(17, ReadPlayerData(PlayerDataKey).Money);
            Assert.IsFalse(PlayerPrefs.HasKey(PlayerDataBackupKey));
        }

        private static PlayerData CreateValidData(int money)
        {
            return new PlayerData
            {
                Money = money,
                CurrentLevel = PlayerProgressTestCatalog.FirstLevelAddress,
                PurchasedSkinIds = new List<int> { 0 },
                QuestStates = new List<Quest>()
            };
        }

        private static PlayerData ReadPlayerData(string key)
        {
            string encryptedData = PlayerPrefs.GetString(key);
            string json = new AesCryptoService().Decrypt(encryptedData);
            return PlayerData.FromJson(json);
        }

        private static void Restore(string key, bool existed, string value)
        {
            if (existed)
            {
                PlayerPrefs.SetString(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
