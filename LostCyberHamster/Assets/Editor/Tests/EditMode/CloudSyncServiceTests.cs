using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.CloudSave;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vues.GameCore;

namespace Assets.Tests.EditMode
{
    public sealed class CloudSyncServiceTests
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

            LevelCatalogService.Reset();
            PlayerPrefs.DeleteKey(PlayerDataKey);
            PlayerPrefs.DeleteKey(PlayerDataBackupKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            GameDataManager.PlayerData = _previousPlayerData;
            LevelCatalogService.Configure(_previousCatalog);
            RestorePreference(PlayerDataKey, _hadPrimary, _savedPrimary);
            RestorePreference(PlayerDataBackupKey, _hadBackup, _savedBackup);
            PlayerPrefs.Save();
        }

        [Test]
        public async Task GuestLinked_PersistsAndUploadsOwnedSnapshotWithoutChangingPlayerId()
        {
            GameDataManager.PlayerData = CreatePlayerData(73);
            var authentication = new FakeAccountAuthenticationGateway
            {
                PlayerId = "guest-player-id"
            };
            var accountService = new AccountService(
                authentication,
                new FakeUnityPlayerAccountGateway());
            var pendingSave = new TaskCompletionSource<CloudSaveWriteResult>();
            var gateway = new FakeCloudSaveGateway
            {
                SaveTask = pendingSave.Task
            };
            var cloudSync = new CloudSyncService(gateway, accountService);
            var serverResult = new CloudSaveWriteResult(
                "server-revision-01",
                new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc));

            accountService.Start();
            var linkResult = await accountService.LinkCurrentGuestAsync();

            Assert.AreEqual(AccountLinkResult.Linked, linkResult);
            Assert.AreEqual("guest-player-id", authentication.PlayerId);
            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.IsTrue(PlayerPrefs.HasKey(PlayerDataKey));
            Assert.AreEqual(ReadSavedPlayerData().ToJson(), gateway.SavedSnapshot.PlayerDataJson);
            Assert.AreEqual("guest-player-id", gateway.SavedSnapshot.PlayerId);
            Assert.AreEqual("1", gateway.SavedSnapshot.Revision);
            Assert.IsNull(gateway.SavedSnapshot.BaseRevision);
            Assert.IsNull(cloudSync.CurrentCloudVersion);
            Assert.IsTrue(cloudSync.HasPendingFirstSnapshot);

            await cloudSync.UploadFirstSnapshotAsync("other-player-id");
            await cloudSync.RetryPendingFirstSnapshotAsync();
            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.AreEqual("guest-player-id", gateway.SavedSnapshot.PlayerId);

            pendingSave.SetResult(serverResult);
            await Task.Yield();

            Assert.AreSame(serverResult, cloudSync.CurrentCloudVersion);
            Assert.IsFalse(cloudSync.HasPendingFirstSnapshot);

            await cloudSync.UploadFirstSnapshotAsync("other-player-id");
            Assert.AreEqual(1, gateway.SaveCallCount);
        }

        [Test]
        public async Task UploadFailure_RetryUsesExactSnapshotAndClearsPendingAfterSuccess()
        {
            GameDataManager.PlayerData = CreatePlayerData(73);
            var gateway = new FakeCloudSaveGateway
            {
                SaveTask = Task.FromException<CloudSaveWriteResult>(
                    new InvalidOperationException("offline"))
            };
            var cloudSync = new CloudSyncService(
                gateway,
                new AccountService(
                    new FakeAccountAuthenticationGateway(),
                    new FakeUnityPlayerAccountGateway()));

            LogAssert.Expect(
                LogType.Error,
                "[CloudSave] First snapshot upload failed (InvalidOperationException).");
            await cloudSync.UploadFirstSnapshotAsync("guest-player-id");

            var firstSnapshot = gateway.SavedSnapshot;
            var firstPayload = firstSnapshot.PlayerDataJson;
            var savedPrimary = PlayerPrefs.GetString(PlayerDataKey);
            Assert.IsTrue(cloudSync.HasPendingFirstSnapshot);
            Assert.IsNull(cloudSync.CurrentCloudVersion);

            GameDataManager.PlayerData.Money = 999;
            var serverResult = new CloudSaveWriteResult(
                "server-revision-02",
                new DateTime(2026, 7, 21, 13, 0, 0, DateTimeKind.Utc));
            gateway.SaveTask = Task.FromResult(serverResult);

            await cloudSync.RetryPendingFirstSnapshotAsync();

            Assert.AreEqual(2, gateway.SaveCallCount);
            Assert.AreSame(firstSnapshot, gateway.SavedSnapshot);
            Assert.AreEqual(firstPayload, gateway.SavedSnapshot.PlayerDataJson);
            Assert.AreEqual(savedPrimary, PlayerPrefs.GetString(PlayerDataKey));
            Assert.AreSame(serverResult, cloudSync.CurrentCloudVersion);
            Assert.IsFalse(cloudSync.HasPendingFirstSnapshot);
        }

        [Test]
        public async Task RepeatedUploadFailure_KeepsPendingAndReloadableLocalProgress()
        {
            GameDataManager.PlayerData = CreatePlayerData(73);
            var gateway = new FakeCloudSaveGateway
            {
                SaveTask = Task.FromException<CloudSaveWriteResult>(
                    new InvalidOperationException("offline"))
            };
            var cloudSync = new CloudSyncService(
                gateway,
                new AccountService(
                    new FakeAccountAuthenticationGateway(),
                    new FakeUnityPlayerAccountGateway()));

            LogAssert.Expect(
                LogType.Error,
                "[CloudSave] First snapshot upload failed (InvalidOperationException).");
            await cloudSync.UploadFirstSnapshotAsync("guest-player-id");
            var firstSnapshot = gateway.SavedSnapshot;

            LogAssert.Expect(
                LogType.Error,
                "[CloudSave] First snapshot upload failed (InvalidOperationException).");
            await cloudSync.RetryPendingFirstSnapshotAsync();

            Assert.AreEqual(2, gateway.SaveCallCount);
            Assert.AreSame(firstSnapshot, gateway.SavedSnapshot);
            Assert.IsTrue(cloudSync.HasPendingFirstSnapshot);
            Assert.IsNull(cloudSync.CurrentCloudVersion);
            GameDataManager.PlayerData = new PlayerData();
            await GameDataManager.LoadDataAsync();
            Assert.AreEqual(73, GameDataManager.PlayerData.Money);
        }

        private static PlayerData CreatePlayerData(int money)
        {
            return new PlayerData
            {
                Money = money,
                Crystals = 4,
                PurchasedSkinIds = new List<int> { 0 },
                DailyTasks = new List<Quest>(),
                StorylineQuestProgress = new List<StorylineQuestProgressEntry>()
            };
        }

        private static PlayerData ReadSavedPlayerData()
        {
            var encrypted = PlayerPrefs.GetString(PlayerDataKey);
            var json = new AesCryptoService().Decrypt(encrypted);
            return PlayerData.FromJson(json);
        }

        private static void RestorePreference(string key, bool existed, string value)
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
