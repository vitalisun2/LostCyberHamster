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

        [Test]
        public async Task LoadExistingAccountAsync_ValidRepairableSnapshot_ReplacesLocalDataAndAcceptsVersion()
        {
            GameDataManager.PlayerData = CreatePlayerData(11);
            GameDataManager.SaveData();
            var cloudData = CreatePlayerData(91);
            cloudData.PurchasedSkinIds = null;
            var readResult = CreateReadResult("linked-player-id", cloudData);
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(readResult)
            };
            var cloudSync = CreateCloudSync(gateway);

            var result = await cloudSync.LoadExistingAccountAsync("linked-player-id");

            Assert.AreEqual(ExistingAccountRestoreResult.Restored, result);
            Assert.AreEqual(91, GameDataManager.PlayerData.Money);
            Assert.AreEqual(91, ReadSavedPlayerData().Money);
            Assert.IsFalse(PlayerPrefs.HasKey(PlayerDataBackupKey));
            CollectionAssert.Contains(GameDataManager.PlayerData.PurchasedSkinIds, 0);
            Assert.AreEqual(readResult.ServerRevision, cloudSync.CurrentCloudVersion.ServerRevision);
            Assert.AreEqual(readResult.ServerModifiedAtUtc, cloudSync.CurrentCloudVersion.ServerModifiedAtUtc);
        }

        [TestCase(ExistingAccountRestoreResult.SnapshotMissing)]
        [TestCase(ExistingAccountRestoreResult.OwnerMismatch)]
        [TestCase(ExistingAccountRestoreResult.SnapshotRejected)]
        public async Task LoadExistingAccountAsync_UnusableSnapshot_PreservesGuestData(
            ExistingAccountRestoreResult expectedResult)
        {
            GameDataManager.PlayerData = CreatePlayerData(11);
            GameDataManager.SaveData();
            var guestData = GameDataManager.PlayerData;
            var savedGuest = PlayerPrefs.GetString(PlayerDataKey);
            var gateway = new FakeCloudSaveGateway();

            if (expectedResult == ExistingAccountRestoreResult.SnapshotMissing)
            {
                gateway.LoadTask = Task.FromResult<CloudSaveReadResult>(null);
                LogAssert.Expect(LogType.Warning, "[CloudSave] Existing account snapshot missing.");
            }
            else if (expectedResult == ExistingAccountRestoreResult.OwnerMismatch)
            {
                gateway.LoadTask = Task.FromResult(CreateReadResult("other-player-id", CreatePlayerData(91)));
                LogAssert.Expect(LogType.Warning, "[CloudSave] Existing account snapshot owner mismatch.");
            }
            else
            {
                var rejectedData = CreatePlayerData(91);
                rejectedData.Money = -1;
                gateway.LoadTask = Task.FromResult(CreateReadResult("linked-player-id", rejectedData));
                LogAssert.Expect(
                    LogType.Warning,
                    "[CloudSave] Existing account snapshot rejected (negative_resource_balance).");
            }

            var cloudSync = CreateCloudSync(gateway);
            var result = await cloudSync.LoadExistingAccountAsync("linked-player-id");

            Assert.AreEqual(expectedResult, result);
            Assert.AreSame(guestData, GameDataManager.PlayerData);
            Assert.AreEqual(savedGuest, PlayerPrefs.GetString(PlayerDataKey));
            Assert.IsNull(cloudSync.CurrentCloudVersion);
        }

        [Test]
        public async Task LoadExistingAccountAsync_LoadError_PreservesGuestData()
        {
            GameDataManager.PlayerData = CreatePlayerData(11);
            GameDataManager.SaveData();
            var guestData = GameDataManager.PlayerData;
            var savedGuest = PlayerPrefs.GetString(PlayerDataKey);
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromException<CloudSaveReadResult>(new InvalidOperationException("offline"))
            };
            var cloudSync = CreateCloudSync(gateway);

            LogAssert.Expect(LogType.Error, "[CloudSave] Existing account load failed (InvalidOperationException).");
            var result = await cloudSync.LoadExistingAccountAsync("linked-player-id");

            Assert.AreEqual(ExistingAccountRestoreResult.LoadFailed, result);
            Assert.AreSame(guestData, GameDataManager.PlayerData);
            Assert.AreEqual(savedGuest, PlayerPrefs.GetString(PlayerDataKey));
            Assert.IsNull(cloudSync.CurrentCloudVersion);
        }

        [Test]
        public async Task ExistingAccountRestoreCoordinator_MissingSnapshot_RestoresGuestWithoutSavingItAgain()
        {
            GameDataManager.PlayerData = CreatePlayerData(11);
            GameDataManager.SaveData();
            var guestData = GameDataManager.PlayerData;
            var savedGuest = PlayerPrefs.GetString(PlayerDataKey);
            var authentication = new FakeAccountAuthenticationGateway();
            var accountService = new AccountService(
                authentication,
                new FakeUnityPlayerAccountGateway());
            accountService.Start();
            var gateway = new FakeCloudSaveGateway();
            var cloudSync = new CloudSyncService(gateway, accountService);
            var coordinator = new ExistingAccountRestoreCoordinator(accountService, cloudSync);

            LogAssert.Expect(LogType.Warning, "[CloudSave] Existing account snapshot missing.");
            LogAssert.Expect(
                LogType.Error,
                "[Account] Existing account sign-in failed. Original guest restored: True. Error type: InvalidOperationException.");
            var result = await coordinator.RestoreAsync();

            Assert.AreEqual(ExistingAccountRestoreResult.SnapshotMissing, result);
            Assert.AreEqual(AccountState.Guest, accountService.State);
            Assert.AreEqual("guest-player-id", authentication.PlayerId);
            Assert.AreSame(guestData, GameDataManager.PlayerData);
            Assert.AreEqual(savedGuest, PlayerPrefs.GetString(PlayerDataKey));
        }

        [Test]
        public async Task ExistingAccountRestoreCoordinator_ValidSnapshot_CompletesLinkedAccountFlow()
        {
            GameDataManager.PlayerData = CreatePlayerData(11);
            GameDataManager.SaveData();
            var authentication = new FakeAccountAuthenticationGateway();
            var accountService = new AccountService(
                authentication,
                new FakeUnityPlayerAccountGateway());
            accountService.Start();
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult("linked-player-id", CreatePlayerData(91)))
            };
            var cloudSync = new CloudSyncService(gateway, accountService);
            var coordinator = new ExistingAccountRestoreCoordinator(accountService, cloudSync);

            var result = await coordinator.RestoreAsync();

            Assert.AreEqual(ExistingAccountRestoreResult.Restored, result);
            Assert.AreEqual(AccountState.Linked, accountService.State);
            Assert.AreEqual("linked-player-id", authentication.PlayerId);
            Assert.AreEqual(91, GameDataManager.PlayerData.Money);
            Assert.AreEqual(91, ReadSavedPlayerData().Money);
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

        private static CloudSyncService CreateCloudSync(FakeCloudSaveGateway gateway)
        {
            return new CloudSyncService(
                gateway,
                new AccountService(
                    new FakeAccountAuthenticationGateway(),
                    new FakeUnityPlayerAccountGateway()));
        }

        private static CloudSaveReadResult CreateReadResult(string playerId, PlayerData data)
        {
            return new CloudSaveReadResult(
                CloudSaveSnapshotCodec.Capture(data, playerId, "cloud-revision"),
                "server-revision-03",
                new DateTime(2026, 7, 21, 14, 0, 0, DateTimeKind.Utc));
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
