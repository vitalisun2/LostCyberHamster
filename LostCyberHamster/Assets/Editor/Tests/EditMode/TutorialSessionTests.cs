using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vues.GameCore.Quests;

namespace Assets.Tests.EditMode
{
    public sealed class TutorialSessionTests
    {
        private const string TutorialBackupKey = "Tutorial.PlayerDataBackup";
        private const string TutorialBackupActiveKey = "Tutorial.PlayerDataBackupActive";
        private const string PlayerDataKey = "PlayerData";
        private const string PlayerDataBackupKey = "PlayerData.Backup";

        private PlayerData _previousPlayerData;
        private HierarchicalLevelCatalog _previousCatalog;
        private readonly Dictionary<string, string> _savedValues = new();
        private readonly HashSet<string> _existingKeys = new();
        private bool _hadTutorialBackupActive;
        private int _tutorialBackupActive;

        [SetUp]
        public void SetUp()
        {
            _previousPlayerData = GameDataManager.PlayerData;
            _previousCatalog = LevelCatalogService.Catalog;
            LevelCatalogService.Reset();
            PreserveKey(TutorialBackupKey);
            _hadTutorialBackupActive = PlayerPrefs.HasKey(TutorialBackupActiveKey);
            _tutorialBackupActive = PlayerPrefs.GetInt(TutorialBackupActiveKey, 0);
            PreserveKey(PlayerDataKey);
            PreserveKey(PlayerDataBackupKey);
            PlayerPrefs.DeleteKey(TutorialBackupKey);
            PlayerPrefs.DeleteKey(TutorialBackupActiveKey);
        }

        [TearDown]
        public void TearDown()
        {
            GameDataManager.PlayerData = _previousPlayerData;
            LevelCatalogService.Configure(_previousCatalog);
            RestoreKey(TutorialBackupKey);
            if (_hadTutorialBackupActive)
            {
                PlayerPrefs.SetInt(TutorialBackupActiveKey, _tutorialBackupActive);
            }
            else
            {
                PlayerPrefs.DeleteKey(TutorialBackupActiveKey);
            }

            RestoreKey(PlayerDataKey);
            RestoreKey(PlayerDataBackupKey);
            PlayerPrefs.Save();
            _savedValues.Clear();
            _existingKeys.Clear();
        }

        [Test]
        public void Complete_RestoresSnapshotAndCommitsFinalTutorialStateOnce()
        {
            GameDataManager.PlayerData = CreateValidPlayerData(money: 71, crystals: 9);
            var session = new TutorialSession();
            session.Begin();
            GameDataManager.PlayerData.Money = 0;
            GameDataManager.PlayerData.Crystals = 0;

            session.Complete(TutorialConstants.FirstGameplayLevelAddress);

            Assert.AreEqual(71, GameDataManager.PlayerData.Money);
            Assert.AreEqual(9, GameDataManager.PlayerData.Crystals);
            Assert.IsTrue(GameDataManager.PlayerData.IsTutorialCompleted);
            Assert.AreEqual(
                TutorialConstants.FirstGameplayLevelAddress,
                GameDataManager.PlayerData.CurrentLevel);
            Assert.IsFalse(TutorialStorage.HasPlayerDataBackup);
            Assert.IsFalse(TutorialStorage.IsPlayerDataBackupActive);
        }

        [Test]
        public void RecoverRejectedSnapshot_PreservesProtectedBackupAndMarker()
        {
            PlayerData rejected = CreateValidPlayerData(money: -1, crystals: 0);
            PlayerPrefs.SetString(TutorialBackupKey, rejected.ToJson());
            PlayerPrefs.SetInt(TutorialBackupActiveKey, 1);
            PlayerPrefs.Save();
            LogAssert.Expect(
                LogType.Error,
                new Regex("Tutorial recovery preserved invalid snapshot"));

            bool recovered = TutorialSession.TryRecoverInterruptedTutorial();

            Assert.IsFalse(recovered);
            Assert.IsTrue(PlayerPrefs.HasKey(TutorialBackupKey));
            Assert.IsTrue(PlayerPrefs.HasKey(TutorialBackupActiveKey));
        }

        [Test]
        public void Begin_ActiveMarkerWithoutBackup_RefusesToReplaceOriginalSnapshot()
        {
            GameDataManager.PlayerData = CreateValidPlayerData(money: 1, crystals: 2);
            PlayerPrefs.SetInt(TutorialBackupActiveKey, 1);
            PlayerPrefs.Save();
            var session = new TutorialSession();

            Assert.Throws<InvalidOperationException>(() => session.Begin());
            Assert.IsFalse(PlayerPrefs.HasKey(TutorialBackupKey));
            Assert.IsTrue(PlayerPrefs.HasKey(TutorialBackupActiveKey));
        }

        private static PlayerData CreateValidPlayerData(int money, int crystals)
        {
            return new PlayerData
            {
                Money = money,
                Crystals = crystals,
                PurchasedSkinIds = new List<int> { 0 },
                AppliedSkinId = 0,
                QuestStates = new List<Quest>()
            };
        }

        private void PreserveKey(string key)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                return;
            }

            _existingKeys.Add(key);
            _savedValues[key] = PlayerPrefs.GetString(key, string.Empty);
        }

        private void RestoreKey(string key)
        {
            if (_existingKeys.Contains(key))
            {
                PlayerPrefs.SetString(key, _savedValues[key]);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
