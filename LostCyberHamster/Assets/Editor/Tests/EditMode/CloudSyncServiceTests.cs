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
        private bool _hadPendingSnapshot;
        private string _savedPendingSnapshot;
        private bool _hadConfirmedVersion;
        private string _savedConfirmedVersion;
        private readonly List<CloudSyncService> _cloudSyncServices = new List<CloudSyncService>();

        [SetUp]
        public void SetUp()
        {
            _previousPlayerData = GameDataManager.PlayerData;
            _previousCatalog = LevelCatalogService.Catalog;
            _hadPrimary = PlayerPrefs.HasKey(PlayerDataKey);
            _savedPrimary = PlayerPrefs.GetString(PlayerDataKey, string.Empty);
            _hadBackup = PlayerPrefs.HasKey(PlayerDataBackupKey);
            _savedBackup = PlayerPrefs.GetString(PlayerDataBackupKey, string.Empty);
            _hadPendingSnapshot = PlayerPrefs.HasKey(CloudPendingSnapshotStore.StorageKey);
            _savedPendingSnapshot = PlayerPrefs.GetString(
                CloudPendingSnapshotStore.StorageKey,
                string.Empty);
            _hadConfirmedVersion = PlayerPrefs.HasKey(
                CloudPendingSnapshotStore.ConfirmedVersionStorageKey);
            _savedConfirmedVersion = PlayerPrefs.GetString(
                CloudPendingSnapshotStore.ConfirmedVersionStorageKey,
                string.Empty);

            LevelCatalogService.Reset();
            PlayerPrefs.DeleteKey(PlayerDataKey);
            PlayerPrefs.DeleteKey(PlayerDataBackupKey);
            PlayerPrefs.DeleteKey(CloudPendingSnapshotStore.StorageKey);
            PlayerPrefs.DeleteKey(CloudPendingSnapshotStore.ConfirmedVersionStorageKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var cloudSyncService in _cloudSyncServices)
                cloudSyncService.Dispose();

            _cloudSyncServices.Clear();
            GameDataManager.PlayerData = _previousPlayerData;
            LevelCatalogService.Configure(_previousCatalog);
            RestorePreference(PlayerDataKey, _hadPrimary, _savedPrimary);
            RestorePreference(PlayerDataBackupKey, _hadBackup, _savedBackup);
            RestorePreference(
                CloudPendingSnapshotStore.StorageKey,
                _hadPendingSnapshot,
                _savedPendingSnapshot);
            RestorePreference(
                CloudPendingSnapshotStore.ConfirmedVersionStorageKey,
                _hadConfirmedVersion,
                _savedConfirmedVersion);
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
            var cloudSync = CreateCloudSync(gateway, accountService);
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
            var cloudSync = CreateCloudSync(
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
            var cloudSync = CreateCloudSync(
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
        public async Task CheckpointQueue_KeepsActiveImmutableAndSendsOnlyNewestPendingSnapshot()
        {
            GameDataManager.PlayerData = CreatePlayerData(10);
            var accountService = CreateResolvedLinkedAccountService("linked-player-id");
            var firstSave = new TaskCompletionSource<CloudSaveWriteResult>();
            var secondSave = new TaskCompletionSource<CloudSaveWriteResult>();
            var localDataAtUpload = new List<string>();
            var gateway = new FakeCloudSaveGateway
            {
                SaveTask = firstSave.Task,
                SaveStarting = _ => localDataAtUpload.Add(ReadSavedPlayerData().ToJson())
            };
            var cloudSync = CreateCloudSync(gateway, accountService);

            PlayerProgressCommitter.Commit(CheckpointReason.MenuEntered);

            var activeSnapshot = gateway.SavedSnapshot;
            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.AreEqual(activeSnapshot.PlayerDataJson, localDataAtUpload[0]);
            Assert.AreEqual(10, CloudSaveSnapshotCodec.RestorePlayerData(activeSnapshot).Money);
            Assert.AreEqual(10, CloudSaveSnapshotCodec.RestorePlayerData(
                CloudPendingSnapshotStore.Load()).Money);

            GameDataManager.PlayerData.Money = 20;
            PlayerProgressCommitter.Commit(CheckpointReason.SkinPurchased);
            GameDataManager.PlayerData.Money = 30;
            PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);

            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.AreEqual(10, CloudSaveSnapshotCodec.RestorePlayerData(activeSnapshot).Money);
            Assert.AreEqual(30, CloudSaveSnapshotCodec.RestorePlayerData(
                CloudPendingSnapshotStore.Load()).Money);

            var firstResult = new CloudSaveWriteResult(
                "server-revision-01",
                new DateTime(2026, 7, 21, 15, 0, 0, DateTimeKind.Utc));
            gateway.SaveTask = secondSave.Task;
            firstSave.SetResult(firstResult);
            await Task.Yield();

            var newestSnapshot = gateway.SavedSnapshot;
            Assert.AreEqual(2, gateway.SaveCallCount);
            Assert.AreNotSame(activeSnapshot, newestSnapshot);
            Assert.AreEqual(30, CloudSaveSnapshotCodec.RestorePlayerData(newestSnapshot).Money);
            Assert.AreEqual("linked-player-id", newestSnapshot.PlayerId);
            Assert.AreEqual(firstResult.ServerRevision, newestSnapshot.BaseRevision);
            Assert.AreEqual(newestSnapshot.PlayerDataJson, localDataAtUpload[1]);
            Assert.AreSame(firstResult, cloudSync.CurrentCloudVersion);
            Assert.AreEqual(firstResult.ServerRevision, CloudPendingSnapshotStore.Load().BaseRevision);

            var secondResult = new CloudSaveWriteResult(
                "server-revision-02",
                new DateTime(2026, 7, 21, 15, 1, 0, DateTimeKind.Utc));
            secondSave.SetResult(secondResult);
            await Task.Yield();

            Assert.AreSame(secondResult, cloudSync.CurrentCloudVersion);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public void CheckpointUpload_RequiresResolvedLinkedAccountAndUsesItsPlayerId()
        {
            GameDataManager.PlayerData = CreatePlayerData(41);
            var authentication = new FakeAccountAuthenticationGateway
            {
                SessionTokenExists = true,
                IsSignedIn = true,
                IsUnityPlayerAccountLinked = true,
                PlayerId = "resolved-player-id"
            };
            var accountService = new AccountService(
                authentication,
                new FakeUnityPlayerAccountGateway());
            var gateway = new FakeCloudSaveGateway();
            CreateCloudSync(gateway, accountService);

            PlayerProgressCommitter.Commit(CheckpointReason.MenuEntered);

            Assert.AreEqual(0, gateway.SaveCallCount);

            accountService.Start();
            PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);

            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.AreEqual("resolved-player-id", gateway.SavedSnapshot.PlayerId);
        }

        [Test]
        public void DurablePending_SurvivesServiceRestartAndUploadsAfterAccountReady()
        {
            GameDataManager.PlayerData = CreatePlayerData(73);
            var firstGateway = new FakeCloudSaveGateway
            {
                SaveTask = Task.FromException<CloudSaveWriteResult>(
                    new InvalidOperationException("offline"))
            };
            var firstService = CreateCloudSync(
                firstGateway,
                CreateResolvedLinkedAccountService("linked-player-id"));

            LogAssert.Expect(
                LogType.Error,
                "[CloudSave] First snapshot upload failed (InvalidOperationException).");
            PlayerProgressCommitter.Commit(CheckpointReason.MenuEntered);

            var storedSnapshot = CloudPendingSnapshotStore.Load();
            Assert.AreEqual(73, CloudSaveSnapshotCodec.RestorePlayerData(storedSnapshot).Money);
            Assert.AreEqual("linked-player-id", storedSnapshot.PlayerId);
            firstService.Dispose();

            var authentication = new FakeAccountAuthenticationGateway
            {
                SessionTokenExists = true,
                IsSignedIn = true,
                IsUnityPlayerAccountLinked = true,
                PlayerId = "linked-player-id"
            };
            var restartedAccountService = new AccountService(
                authentication,
                new FakeUnityPlayerAccountGateway());
            var restartedGateway = new FakeCloudSaveGateway();
            CreateCloudSync(restartedGateway, restartedAccountService);

            Assert.AreEqual(0, restartedGateway.SaveCallCount);

            restartedAccountService.Start();

            Assert.AreEqual(1, restartedGateway.SaveCallCount);
            Assert.AreEqual("linked-player-id", restartedGateway.SavedSnapshot.PlayerId);
            Assert.AreEqual(storedSnapshot.Revision, restartedGateway.SavedSnapshot.Revision);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public void DurablePending_DoesNotUploadForDifferentResolvedOwner()
        {
            var storedSnapshot = CloudSaveSnapshotCodec.Capture(
                CreatePlayerData(52),
                "first-player-id",
                "7");
            CloudPendingSnapshotStore.Save(storedSnapshot);
            var accountService = CreateResolvedLinkedAccountService("second-player-id");
            var gateway = new FakeCloudSaveGateway();

            CreateCloudSync(gateway, accountService);

            Assert.AreEqual(0, gateway.SaveCallCount);
            Assert.AreEqual("first-player-id", CloudPendingSnapshotStore.Load().PlayerId);
        }

        [Test]
        public void ApplicationResume_RetriesDurablePendingThroughQueue()
        {
            CloudPendingSnapshotStore.Save(CloudSaveSnapshotCodec.Capture(
                CreatePlayerData(61),
                "linked-player-id",
                "4"));
            var gateway = new FakeCloudSaveGateway
            {
                SaveTask = Task.FromException<CloudSaveWriteResult>(
                    new InvalidOperationException("offline"))
            };

            LogAssert.Expect(
                LogType.Error,
                "[CloudSave] First snapshot upload failed (InvalidOperationException).");
            CreateCloudSync(
                gateway,
                CreateResolvedLinkedAccountService("linked-player-id"));
            Assert.AreEqual(1, gateway.SaveCallCount);

            gateway.SaveTask = Task.FromResult(new CloudSaveWriteResult(
                "server-revision-resumed",
                new DateTime(2026, 7, 21, 16, 0, 0, DateTimeKind.Utc)));
            PlayerProgressLifecycleCheckpoint.HandleApplicationPause(isPaused: false);

            Assert.AreEqual(2, gateway.SaveCallCount);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public void LocalOnlyChange_WithMatchingBase_UploadsWithoutConflict()
        {
            var localSnapshot = CreateSnapshot("linked-player-id", 20, "2", "base-revision");
            CloudPendingSnapshotStore.Save(localSnapshot);
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult(
                    CreateSnapshot("linked-player-id", 10, "1", null),
                    "base-revision"))
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);

            accountService.Start();

            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.AreEqual("base-revision", gateway.SavedSnapshot.BaseRevision);
            Assert.AreEqual(20, CloudSaveSnapshotCodec.RestorePlayerData(gateway.SavedSnapshot).Money);
            Assert.IsNull(cloudSync.CurrentConflict);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public void CloudOnlyChange_WithoutPending_AppliesWithoutConflict()
        {
            GameDataManager.PlayerData = CreatePlayerData(10);
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult(
                    CreateSnapshot("linked-player-id", 30, "cloud-2", "base-revision"),
                    "cloud-revision"))
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);

            accountService.Start();

            Assert.AreEqual(30, GameDataManager.PlayerData.Money);
            Assert.AreEqual(0, gateway.SaveCallCount);
            Assert.IsNull(cloudSync.CurrentConflict);
            Assert.AreEqual("cloud-revision", cloudSync.CurrentCloudVersion.ServerRevision);
        }

        [Test]
        public void DivergedLocalAndCloud_FromCommonBase_RaisesConflictWithoutUpload()
        {
            CloudPendingSnapshotStore.Save(
                CreateSnapshot("linked-player-id", 20, "local-2", "base-revision"));
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult(
                    CreateSnapshot("linked-player-id", 30, "cloud-2", "base-revision"),
                    "cloud-revision"))
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);
            CloudSaveConflict raisedConflict = null;
            cloudSync.ConflictDetected += conflict => raisedConflict = conflict;

            accountService.Start();

            Assert.AreSame(cloudSync.CurrentConflict, raisedConflict);
            Assert.AreEqual(20, CloudSaveSnapshotCodec.RestorePlayerData(
                raisedConflict.LocalSnapshot).Money);
            Assert.AreEqual(30, CloudSaveSnapshotCodec.RestorePlayerData(
                raisedConflict.CloudSnapshot).Money);
            Assert.AreEqual(0, gateway.SaveCallCount);
            Assert.IsNotNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public void LostAcknowledgement_EquivalentCloudSnapshot_ClearsPendingWithoutConflict()
        {
            var pending = CreateSnapshot("linked-player-id", 20, "local-2", "base-revision");
            CloudPendingSnapshotStore.Save(pending);
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult(
                    CloudSaveSnapshotCodec.Deserialize(CloudSaveSnapshotCodec.Serialize(pending)),
                    "acknowledged-revision"))
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);

            accountService.Start();

            Assert.AreEqual(0, gateway.SaveCallCount);
            Assert.IsNull(cloudSync.CurrentConflict);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
            Assert.AreEqual("acknowledged-revision", cloudSync.CurrentCloudVersion.ServerRevision);
        }

        [Test]
        public async Task CloudConflictChoice_WhenCloudChanged_RefreshesConflictAndRequiresRechoice()
        {
            GameDataManager.PlayerData = CreatePlayerData(20);
            CloudPendingSnapshotStore.Save(
                CreateSnapshot("linked-player-id", 20, "local-2", "base-revision"));
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult(
                    CreateSnapshot("linked-player-id", 30, "cloud-2", "base-revision"),
                    "cloud-revision-1"))
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);
            var raisedCount = 0;
            cloudSync.ConflictDetected += _ => raisedCount++;
            accountService.Start();
            gateway.LoadTask = Task.FromResult(CreateReadResult(
                CreateSnapshot("linked-player-id", 40, "cloud-3", "cloud-revision-1"),
                "cloud-revision-2"));

            var resolved = await cloudSync.ResolveConflictWithCloudAsync();

            Assert.IsFalse(resolved);
            Assert.AreEqual(2, raisedCount);
            Assert.AreEqual("cloud-revision-2", cloudSync.CurrentConflict.CloudVersion.ServerRevision);
            Assert.AreEqual(40, CloudSaveSnapshotCodec.RestorePlayerData(
                cloudSync.CurrentConflict.CloudSnapshot).Money);
            Assert.AreEqual(20, GameDataManager.PlayerData.Money);
        }

        [Test]
        public async Task LocalConflictChoice_ReloadsLatestRevisionAndWritesWholeLocalSnapshot()
        {
            CloudPendingSnapshotStore.Save(
                CreateSnapshot("linked-player-id", 20, "local-2", "base-revision"));
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult(CreateReadResult(
                    CreateSnapshot("linked-player-id", 30, "cloud-2", "base-revision"),
                    "cloud-revision-1"))
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);
            accountService.Start();
            gateway.LoadTask = Task.FromResult(CreateReadResult(
                CreateSnapshot("linked-player-id", 40, "cloud-3", "cloud-revision-1"),
                "cloud-revision-2"));
            gateway.SaveTask = Task.FromResult(new CloudSaveWriteResult(
                "local-choice-revision",
                new DateTime(2026, 7, 21, 17, 0, 0, DateTimeKind.Utc)));

            var resolved = await cloudSync.ResolveConflictWithLocalAsync();

            Assert.IsTrue(resolved);
            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.AreEqual("cloud-revision-2", gateway.SavedSnapshot.BaseRevision);
            Assert.AreEqual(20, CloudSaveSnapshotCodec.RestorePlayerData(
                gateway.SavedSnapshot).Money);
            Assert.IsNull(cloudSync.CurrentConflict);
            Assert.AreEqual("local-choice-revision", cloudSync.CurrentCloudVersion.ServerRevision);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public async Task KnownBaseWithMissingCloud_RetainsPendingThenRecreatesOnConfirmedRetry()
        {
            CloudPendingSnapshotStore.Save(
                CreateSnapshot("linked-player-id", 20, "local-2", "base-revision"));
            SaveConfirmedVersion("linked-player-id", "base-revision");
            var gateway = new FakeCloudSaveGateway
            {
                LoadTask = Task.FromResult<CloudSaveReadResult>(null)
            };
            var accountService = CreateUnstartedLinkedAccountService("linked-player-id");
            var cloudSync = CreateCloudSync(gateway, accountService);

            LogAssert.Expect(
                LogType.Error,
                "[CloudSave] Pending base is missing in cloud; retry required.");
            accountService.Start();

            Assert.AreEqual(0, gateway.SaveCallCount);
            Assert.AreEqual("base-revision", CloudPendingSnapshotStore.Load().BaseRevision);
            Assert.IsNull(cloudSync.CurrentConflict);

            await cloudSync.RetryPendingFirstSnapshotAsync();

            Assert.AreEqual(1, gateway.SaveCallCount);
            Assert.IsNull(gateway.SavedSnapshot.BaseRevision);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
        }

        [Test]
        public async Task LoadExistingAccountAsync_ValidRepairableSnapshot_ReplacesLocalDataAndAcceptsVersion()
        {
            GameDataManager.PlayerData = CreatePlayerData(11);
            GameDataManager.SaveData();
            CloudPendingSnapshotStore.Save(
                CreateSnapshot("linked-player-id", 17, "local-2", "base-revision"));
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
            Assert.IsNull(cloudSync.CurrentConflict);
            Assert.IsNull(CloudPendingSnapshotStore.Load());
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
            var cloudSync = CreateCloudSync(gateway, accountService);
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
            var cloudSync = CreateCloudSync(gateway, accountService);
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

        private AccountService CreateResolvedLinkedAccountService(string playerId)
        {
            var accountService = CreateUnstartedLinkedAccountService(playerId);
            accountService.Start();
            return accountService;
        }

        private static AccountService CreateUnstartedLinkedAccountService(string playerId)
        {
            return new AccountService(
                new FakeAccountAuthenticationGateway
                {
                    SessionTokenExists = true,
                    IsSignedIn = true,
                    IsUnityPlayerAccountLinked = true,
                    PlayerId = playerId
                },
                new FakeUnityPlayerAccountGateway());
        }

        private CloudSyncService CreateCloudSync(FakeCloudSaveGateway gateway)
        {
            return CreateCloudSync(
                gateway,
                new AccountService(
                    new FakeAccountAuthenticationGateway(),
                    new FakeUnityPlayerAccountGateway()));
        }

        private CloudSyncService CreateCloudSync(
            FakeCloudSaveGateway gateway,
            AccountService accountService)
        {
            var cloudSyncService = new CloudSyncService(gateway, accountService);
            _cloudSyncServices.Add(cloudSyncService);
            return cloudSyncService;
        }

        private static CloudSaveReadResult CreateReadResult(string playerId, PlayerData data)
        {
            return new CloudSaveReadResult(
                CloudSaveSnapshotCodec.Capture(data, playerId, "cloud-revision"),
                "server-revision-03",
                new DateTime(2026, 7, 21, 14, 0, 0, DateTimeKind.Utc));
        }

        private static CloudSaveReadResult CreateReadResult(
            CloudSaveSnapshot snapshot,
            string serverRevision)
        {
            return new CloudSaveReadResult(
                snapshot,
                serverRevision,
                new DateTime(2026, 7, 21, 14, 0, 0, DateTimeKind.Utc));
        }

        private static CloudSaveSnapshot CreateSnapshot(
            string playerId,
            int money,
            string revision,
            string baseRevision)
        {
            return CloudSaveSnapshotCodec.Capture(
                CreatePlayerData(money),
                playerId,
                revision,
                baseRevision);
        }

        private static void SaveConfirmedVersion(string playerId, string serverRevision)
        {
            CloudPendingSnapshotStore.SaveConfirmedVersion(
                playerId,
                new CloudSaveWriteResult(
                    serverRevision,
                    new DateTime(2026, 7, 21, 13, 0, 0, DateTimeKind.Utc)));
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
