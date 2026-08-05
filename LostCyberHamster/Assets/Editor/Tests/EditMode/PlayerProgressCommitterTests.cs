using Assets.Scripts.System;
using GameManagement;
using NUnit.Framework;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Tests.EditMode
{
    public sealed class PlayerProgressCommitterTests
    {
        private const string PlayerDataKey = "PlayerData";
        private const string PlayerDataBackupKey = "PlayerData.Backup";

        private PlayerData _previousPlayerData;
        private HierarchicalLevelCatalog _previousCatalog;
        private bool _hadSavedPlayerData;
        private string _savedPlayerData;
        private bool _hadSavedBackup;
        private string _savedBackup;

        [SetUp]
        public void SetUp()
        {
            _previousPlayerData = GameDataManager.PlayerData;
            _previousCatalog = LevelCatalogService.Catalog;
            _hadSavedPlayerData = PlayerPrefs.HasKey(PlayerDataKey);
            _savedPlayerData = PlayerPrefs.GetString(PlayerDataKey, string.Empty);
            _hadSavedBackup = PlayerPrefs.HasKey(PlayerDataBackupKey);
            _savedBackup = PlayerPrefs.GetString(PlayerDataBackupKey, string.Empty);
            LevelCatalogService.Configure(PlayerProgressTestCatalog.Create());
        }

        [TearDown]
        public void TearDown()
        {
            GameDataManager.PlayerData = _previousPlayerData;
            LevelCatalogService.Configure(_previousCatalog);

            if (_hadSavedPlayerData)
            {
                PlayerPrefs.SetString(PlayerDataKey, _savedPlayerData);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayerDataKey);
            }

            if (_hadSavedBackup)
            {
                PlayerPrefs.SetString(PlayerDataBackupKey, _savedBackup);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayerDataBackupKey);
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void Commit_PersistsCurrentPlayerDataSnapshot()
        {
            GameDataManager.PlayerData = new PlayerData
            {
                Money = 27,
                Crystals = 4,
                CurrentLevel = PlayerProgressTestCatalog.FirstLevelAddress
            };

            PlayerProgressCommitter.Commit(CheckpointReason.MenuEntered);

            var encryptedData = PlayerPrefs.GetString(PlayerDataKey);
            var json = new AesCryptoService().Decrypt(encryptedData);
            var restored = PlayerData.FromJson(json);

            Assert.AreEqual(27, restored.Money);
            Assert.AreEqual(4, restored.Crystals);
        }
    }
}
