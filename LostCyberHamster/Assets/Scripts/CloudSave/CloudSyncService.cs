using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Online;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Сверяет durable локальную revision с облаком через общий foreground retry.</summary>
    public sealed class CloudSyncService : IDisposable
    {
        private const string RetryJob = "cloud-save";
        private readonly AccountService _accountService;
        private readonly ICloudSaveGateway _gateway;
        private readonly ConflictService _conflictService;
        private readonly IDisposable _retryRegistration;
        private bool _isSynchronizationActive;
        private bool _isDisposed;
        private bool _lastAttemptFailed;
        private int _accountLifecycleVersion;
        private string _reconciledPlayerId;
        private long _reconciledGeneration = -1;
        private bool _cloudRefreshPending;
        private int _choiceVersion;

        public CloudSyncService(AccountService accountService, ICloudSaveVersionStore versionStore,
            ICloudSaveGateway gateway, SnapshotService snapshotService, ConflictService conflictService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));
            _retryRegistration = OnlineServicesCoordinator.Register(RetryJob, SynchronizeAsync, CanSynchronize);
            _accountService.StateChanged += OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed += OnApplicationResumed;
            GameDataManager.ProfileChanged += OnProfileChanged;
            _conflictService.ConflictResolved += OnConflictResolved;
        }

        public event Action<CloudSyncStatusEnum> StatusChanged;
        public event Action ConflictPresentationRequested;
        public bool HasUnresolvedConflict => _conflictService.CurrentConflict != null;
        public bool IsInitialReconciliationComplete => GameDataManager.OwnerPlayerId == _reconciledPlayerId &&
            GameDataManager.Generation == _reconciledGeneration;
        public bool IsConflictDeferred => _conflictService.CurrentConflict != null &&
            GameDataManager.IsConflictDeferred(_conflictService.CurrentConflict.LocalSnapshot.PlayerId,
                _conflictService.CurrentConflict.CloudRevision);
        public string LastError { get; private set; }

        public CloudSyncStatusEnum Status
        {
            get
            {
                if (HasUnresolvedConflict) return CloudSyncStatusEnum.Conflict;
                if (_isSynchronizationActive) return CloudSyncStatusEnum.Synchronizing;
                if (!_accountService.TryGetLinkedPlayerId(out _))
                    return _accountService.HasKnownLinkedIdentity ? CloudSyncStatusEnum.Pending : CloudSyncStatusEnum.LocalOnly;
                if (GameDataManager.HasUnsyncedProgress || _cloudRefreshPending) return CloudSyncStatusEnum.Pending;
                return _lastAttemptFailed ? CloudSyncStatusEnum.Unavailable : CloudSyncStatusEnum.Saved;
            }
        }

        private bool CanSynchronize() => !_isDisposed && GameDataManager.IsLoaded &&
            OnlineServicesCoordinator.UnityServicesReady && _accountService.TryGetLinkedPlayerId(out _) &&
            !_conflictService.IsResolutionActive && !GameDataManager.IsProfileReplacementBlocked;

        private void OnAccountStateChanged(AccountState state)
        {
            _accountLifecycleVersion++;
            _reconciledGeneration = -1;
            RestoreConflictForCurrentProfile();
            _lastAttemptFailed = false;
            RequestRetry();
        }

        private void OnProfileChanged()
        {
            _reconciledGeneration = -1;
            RestoreConflictForCurrentProfile();
            RequestRetry();
        }

        private void RestoreConflictForCurrentProfile()
        {
            if (!GameDataManager.IsLoaded) return;
            var owner = _accountService.TryGetAuthenticatedPlayerId(out var authenticatedOwner)
                ? authenticatedOwner : GameDataManager.OwnerPlayerId ?? GameDataManager.ActiveConflictOwner;
            _conflictService.RestoreForOwner(owner);
        }
        private void OnCheckpointCommitted(CheckpointReason reason)
        {
            if (_isDisposed) return;
            if (HasUnresolvedConflict)
                _conflictService.TryUpdateLocalSnapshot(new CloudSaveSnapshot(
                    _conflictService.CurrentConflict.LocalSnapshot.PlayerId, GameDataManager.GetSavedPlayerDataJson()));
            RequestRetry();
        }
        private void OnApplicationResumed() => RequestRetry();
        private void OnConflictResolved() => RequestRetry();

        /// <summary>Запрашивает повтор без прямого сетевого вызова из gameplay.</summary>
        public void RequestRetry()
        {
            if (_isDisposed) return;
            OnlineServicesCoordinator.RequestRetry(RetryJob);
            NotifyStatusChanged();
        }

        /// <summary>Оставляет обе ветки и продолжает локальную игру.</summary>
        public void DeferConflict()
        {
            var conflict = _conflictService.CurrentConflict;
            if (conflict == null) return;
            CancelConflictChoice();
            GameDataManager.SetConflictDeferred(conflict.LocalSnapshot.PlayerId, conflict.CloudRevision);
            NotifyStatusChanged();
        }

        /// <summary>Инвалидирует результат чтения выбора; уже отправленный upload восстановится по durable attempt.</summary>
        public void CancelConflictChoice() => _choiceVersion++;

        /// <summary>Открывает отложенный выбор по явному действию игрока.</summary>
        public void ShowConflict()
        {
            if (!HasUnresolvedConflict) { RequestRetry(); return; }
            GameDataManager.SetConflictDeferred(null, null);
            ConflictPresentationRequested?.Invoke();
        }

        private async Task SynchronizeAsync()
        {
            if (!CanSynchronize() || _isSynchronizationActive) return;
            _isSynchronizationActive = true;
            NotifyStatusChanged();
            try
            {
                while (CanSynchronize())
                {
                    if (!_accountService.TryGetLinkedPlayerId(out var playerId)) return;
                    GameDataManager.TryBindAuthenticatedOwner(playerId);
                    if (GameDataManager.OwnerPlayerId != null && GameDataManager.OwnerPlayerId != playerId)
                        throw new InvalidOperationException("Local progress belongs to another account.");
                    var lifecycle = _accountLifecycleVersion;
                    var generation = GameDataManager.Generation;
                    var startingRevision = GameDataManager.LocalRevision;
                    var cloud = await _gateway.LoadSnapshotAsync();
                    if (!IsCurrent(playerId, lifecycle) || GameDataManager.Generation != generation) return;
                    if (_conflictService.IsResolutionActive) return;
                    EnsureOwner(playerId, cloud);
                    _lastAttemptFailed = false;
                    LastError = null;

                    // Legacy не получает owner из случайно восстановленных credentials.
                    if (GameDataManager.OwnerPlayerId == null)
                    {
                        _conflictService.SetConflict(Capture(playerId), cloud);
                        return;
                    }

                    // Подтверждаем старую отправку, даже если после неё уже появились новые edits.
                    var attempt = GameDataManager.LastUploadAttempt;
                    var recoveredUpload = cloud != null && attempt != null && attempt.OwnerPlayerId == playerId &&
                        attempt.ProfileId == GameDataManager.ProfileId &&
                        attempt.PayloadHash == CloudSaveSnapshot.ComputePayloadHash(cloud.Snapshot.PlayerDataJson);
                    if (recoveredUpload)
                    {
                        GameDataManager.AcknowledgeCloudUpload(attempt, cloud.Version.ServerRevision);
                        _conflictService.ClearConflict();
                    }
                    else if (HasUnresolvedConflict)
                    {
                        // Обновляем только показанные ветки; Later не разрешает overwrite.
                        _conflictService.SetConflict(Capture(playerId), cloud);
                        return;
                    }

                    var cloudRevision = cloud?.Version.ServerRevision;
                    var changedInCloud = !string.Equals(cloudRevision, GameDataManager.BaseCloudRevision, StringComparison.Ordinal);
                    if (cloud == null)
                    {
                        if (GameDataManager.BaseCloudRevision != null)
                        {
                            _conflictService.SetConflict(Capture(playerId), null);
                            return;
                        }
                        await UploadAsync(playerId, null, lifecycle, generation);
                    }
                    else if (changedInCloud)
                    {
                        if (GameDataManager.HasUnsyncedProgress || startingRevision != GameDataManager.LocalRevision)
                        {
                            _conflictService.SetConflict(Capture(playerId), cloud);
                            return;
                        }
                        if (!GameDataManager.CanApplyCloudProgress)
                        {
                            _cloudRefreshPending = true;
                            return;
                        }
                        GameDataManager.ApplyCloudPlayerData(PlayerData.FromJson(cloud.Snapshot.PlayerDataJson),
                            playerId, cloudRevision);
                        MarkReconciled(playerId);
                        return;
                    }
                    else if (GameDataManager.HasUnsyncedProgress)
                        await UploadAsync(playerId, cloudRevision, lifecycle, generation);
                    else { MarkReconciled(playerId); return; }

                    if (!IsCurrent(playerId, lifecycle)) return;
                    if (!GameDataManager.HasUnsyncedProgress) { MarkReconciled(playerId); return; }
                }
            }
            catch (Exception exception)
            {
                _lastAttemptFailed = true;
                LastError = exception.GetType().Name;
                throw;
            }
            finally
            {
                _isSynchronizationActive = false;
                NotifyStatusChanged();
            }
        }

        private async Task UploadAsync(string playerId, string expectedRevision, int lifecycle, long generation)
        {
            var snapshot = Capture(playerId);
            var attempt = new CloudUploadAttempt
            {
                ProfileId = GameDataManager.ProfileId,
                OwnerPlayerId = playerId,
                LocalRevision = GameDataManager.LocalRevision,
                PayloadHash = CloudSaveSnapshot.ComputePayloadHash(snapshot.PlayerDataJson),
                ExpectedCloudRevision = expectedRevision
            };
            GameDataManager.RecordCloudUploadAttempt(attempt);
            var acknowledgement = await _gateway.SaveSnapshotAsync(snapshot, expectedRevision);
            if (!IsCurrent(playerId, lifecycle) || GameDataManager.Generation != generation) return;
            if (acknowledgement == null) throw new InvalidOperationException("Cloud upload has no acknowledgement.");
            GameDataManager.AcknowledgeCloudUpload(attempt, acknowledgement.ServerRevision);
        }

        /// <summary>Применяет выбранный существующий аккаунт только после полной сетевой проверки.</summary>
        public async Task RestoreProgressAsync(string playerId)
        {
            var lifecycle = _accountLifecycleVersion;
            var generation = GameDataManager.Generation;
            var revision = GameDataManager.LocalRevision;
            if (!_accountService.IsCurrentPlayer(playerId, allowSigningIn: true))
                throw new InvalidOperationException("Account restore is stale.");
            var cloud = await _gateway.LoadSnapshotAsync();
            if (cloud == null) throw new InvalidOperationException("Existing account cloud snapshot is missing.");
            EnsureOwner(playerId, cloud);
            if (lifecycle != _accountLifecycleVersion || generation != GameDataManager.Generation ||
                revision != GameDataManager.LocalRevision || !_accountService.IsCurrentPlayer(playerId, allowSigningIn: true))
                throw new InvalidOperationException("Local progress changed during account restore.");
            GameDataManager.ApplyCloudPlayerData(PlayerData.FromJson(cloud.Snapshot.PlayerDataJson), playerId,
                cloud.Version.ServerRevision);
            _conflictService.ClearConflict();
            MarkReconciled(playerId);
            NotifyStatusChanged();
        }

        public Task<bool> ResolveConflictWithCloudAsync() => ResolveConflictAsync(useCloud: true);
        public Task<bool> ResolveConflictWithLocalAsync() => ResolveConflictAsync(useCloud: false);

        private async Task<bool> ResolveConflictAsync(bool useCloud)
        {
            if (_isDisposed || !_accountService.TryGetLinkedPlayerId(out var playerId)) return false;
            var lifecycle = _accountLifecycleVersion;
            var choiceVersion = ++_choiceVersion;
            var result = useCloud
                ? await _conflictService.ResolveWithCloudAsync(playerId, () => IsCurrent(playerId, lifecycle) && choiceVersion == _choiceVersion)
                : await _conflictService.ResolveWithLocalAsync(playerId, () => IsCurrent(playerId, lifecycle) && choiceVersion == _choiceVersion);
            LastError = result ? null : _conflictService.LastResolutionError;
            if (result) MarkReconciled(playerId);
            NotifyStatusChanged();
            return result;
        }

        private bool IsCurrent(string playerId, int lifecycle) => !_isDisposed &&
            lifecycle == _accountLifecycleVersion && _accountService.IsCurrentPlayer(playerId, allowSigningIn: false);
        private static CloudSaveSnapshot Capture(string playerId) => new CloudSaveSnapshot(playerId, GameDataManager.GetSavedPlayerDataJson());

        private void MarkReconciled(string playerId)
        {
            _reconciledPlayerId = playerId;
            _reconciledGeneration = GameDataManager.Generation;
            _cloudRefreshPending = false;
        }
        private static void EnsureOwner(string playerId, CloudSaveReadResult cloud)
        {
            if (cloud != null && cloud.Snapshot.PlayerId != playerId)
                throw new InvalidOperationException("Cloud snapshot owner mismatch.");
        }

        private void NotifyStatusChanged()
        {
            var handlers = StatusChanged;
            if (handlers == null) return;
            foreach (Action<CloudSyncStatusEnum> handler in handlers.GetInvocationList())
            {
                try { handler(Status); }
                catch (Exception exception) { Debug.LogError($"[CloudSave] Status subscriber failed ({exception.GetType().Name})."); }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _accountLifecycleVersion++;
            _retryRegistration.Dispose();
            _accountService.StateChanged -= OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted -= OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed -= OnApplicationResumed;
            GameDataManager.ProfileChanged -= OnProfileChanged;
            _conflictService.ConflictResolved -= OnConflictResolved;
        }
    }
}
