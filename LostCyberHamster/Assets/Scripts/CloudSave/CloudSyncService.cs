using System;
using System.Globalization;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Создаёт облачные снимки и последовательно отправляет последний checkpoint.
    /// </summary>
    public sealed class CloudSyncService : IDisposable
    {
        private readonly ICloudSaveGateway _gateway;
        private readonly AccountService _accountService;
        private readonly CloudSaveConflictService _conflictService;
        private CloudSaveSnapshot _pendingSnapshot;
        private CloudSaveSnapshot _firstSnapshotAwaitingConfirmation;
        private bool _isSnapshotUploadActive;
        private bool _isCloudRefreshActive;
        private string _currentCloudVersionPlayerId;
        private string _missingCloudPendingPlayerId;
        private string _missingCloudPendingRevision;
        private long _nextLocalRevision = 1;

        /// <summary>Восстанавливает durable pending и подписывает очередь на account/lifecycle события.</summary>
        public CloudSyncService(
            ICloudSaveGateway gateway,
            AccountService accountService,
            CloudSaveConflictService conflictService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (accountService == null)
            {
                throw new ArgumentNullException(nameof(accountService));
            }

            _accountService = accountService;
            _conflictService = conflictService
                ?? throw new ArgumentNullException(nameof(conflictService));
            _pendingSnapshot = CloudPendingSnapshotStore.Load();
            AdvanceLocalRevisionPast(_pendingSnapshot);
            accountService.CurrentGuestLinked += OnCurrentGuestLinked;
            accountService.StateChanged += OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed += OnApplicationResumed;
            TryUploadPendingForCurrentAccount();
        }

        /// <summary>Последняя подтверждённая сервером версия текущего процесса игры.</summary>
        public CloudSaveWriteResult CurrentCloudVersion { get; private set; }

        /// <summary>Текущие две независимо изменённые ветки, ожидающие выбора.</summary>
        public CloudSaveConflictModel CurrentConflict => _conflictService.CurrentConflict;

        /// <summary>Возникает при обнаружении или обновлении данных конфликта.</summary>
        public event Action<CloudSaveConflictModel> ConflictDetected
        {
            add => _conflictService.ConflictDetected += value;
            remove => _conflictService.ConflictDetected -= value;
        }

        /// <summary>Есть первый снимок, который облако ещё не подтвердило.</summary>
        public bool HasPendingFirstSnapshot => _firstSnapshotAwaitingConfirmation != null;

        /// <summary>
        /// Сначала сохраняет полный прогресс локально, затем отправляет его первый снимок в облако.
        /// </summary>
        public async Task UploadFirstSnapshotAsync(string playerId)
        {
            if (_isSnapshotUploadActive ||
                _pendingSnapshot != null ||
                _firstSnapshotAwaitingConfirmation != null ||
                CurrentCloudVersion != null)
            {
                Debug.Log("[CloudSave] First snapshot upload skipped: already started.");
                return;
            }

            try
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AccountLinked);
                Debug.Log("[CloudSave] Local commit completed: AccountLinked.");

                var snapshot = CloudSaveSnapshotCodec.Capture(
                    GameDataManager.PlayerData,
                    playerId,
                    GetNextLocalRevision());
                _firstSnapshotAwaitingConfirmation = snapshot;
                _pendingSnapshot = snapshot;
                CloudPendingSnapshotStore.Save(snapshot);

                await UploadPendingSnapshotAsync(isRetry: false);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] First snapshot upload failed ({exception.GetType().Name}).");
            }
        }

        /// <summary>Повторно отправляет тот же неподтверждённый снимок.</summary>
        public Task RetryPendingFirstSnapshotAsync()
        {
            if (_isSnapshotUploadActive)
            {
                Debug.Log("[CloudSave] First snapshot retry skipped: upload active.");
                return Task.CompletedTask;
            }

            if (_pendingSnapshot == null)
            {
                Debug.Log("[CloudSave] First snapshot retry skipped: no pending snapshot.");
                return Task.CompletedTask;
            }

            return UploadPendingSnapshotAsync(isRetry: true);
        }

        /// <summary>Проверяет актуальность выбранного cloud snapshot и целиком применяет его локально.</summary>
        public async Task<bool> ResolveConflictWithCloudAsync()
        {
            var conflict = CurrentConflict;
            var latestCloud = await _conflictService.ResolveWithCloudAsync();
            if (conflict == null || latestCloud == null)
                return false;

            try
            {
                var playerId = conflict.LocalSnapshot.PlayerId;
                DiscardPendingForOwner(playerId);
                SetCurrentCloudVersion(playerId, latestCloud);
                return true;
            }
            catch (Exception exception)
            {
                _conflictService.SetConflict(
                    _pendingSnapshot ?? conflict.LocalSnapshot,
                    latestCloud);
                Debug.LogError($"[CloudSave] Cloud conflict choice failed ({exception.GetType().Name}).");
                return false;
            }
        }

        /// <summary>Записывает выбранный local snapshot целиком поверх актуальной cloud revision.</summary>
        public async Task<bool> ResolveConflictWithLocalAsync()
        {
            var conflict = CurrentConflict;
            var result = await _conflictService.ResolveWithLocalAsync();
            if (conflict == null)
                return false;

            if (result == null)
            {
                if (IsSamePending(_pendingSnapshot, conflict.LocalSnapshot))
                    _pendingSnapshot = conflict.LocalSnapshot;

                return false;
            }

            try
            {
                SetCurrentCloudVersion(conflict.LocalSnapshot.PlayerId, result);
                CloudPendingSnapshotStore.ClearIfMatches(conflict.LocalSnapshot);

                if (IsSamePending(_pendingSnapshot, conflict.LocalSnapshot))
                    _pendingSnapshot = null;

                RebasePendingTo(result.ServerRevision);
                if (_pendingSnapshot != null)
                    _ = UploadPendingSnapshotAsync(isRetry: false);

                return true;
            }
            catch (Exception exception)
            {
                if (_pendingSnapshot == null)
                    _pendingSnapshot = conflict.LocalSnapshot;

                _conflictService.SetConflict(_pendingSnapshot, conflict.CloudVersion);

                Debug.LogError($"[CloudSave] Local conflict choice failed ({exception.GetType().Name}).");
                return false;
            }
        }

        /// <summary>
        /// Загружает и целиком применяет снимок подтверждённого существующего аккаунта.
        /// </summary>
        public async Task<ExistingAccountRestoreResult> LoadExistingAccountAsync(string playerId)
        {
            CloudSaveReadResult readResult;
            try
            {
                readResult = await _gateway.LoadSnapshotAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Existing account load failed ({exception.GetType().Name}).");
                return ExistingAccountRestoreResult.LoadFailed;
            }

            if (readResult == null)
            {
                Debug.LogWarning("[CloudSave] Existing account snapshot missing.");
                return ExistingAccountRestoreResult.SnapshotMissing;
            }

            if (!string.Equals(readResult.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[CloudSave] Existing account snapshot owner mismatch.");
                return ExistingAccountRestoreResult.OwnerMismatch;
            }

            if (!CloudSaveConflictService.TryRestoreValidatedPlayerData(
                    readResult.Snapshot,
                    out var restoredData,
                    out var rejectionReason))
            {
                Debug.LogWarning($"[CloudSave] Existing account snapshot rejected ({rejectionReason}).");
                return ExistingAccountRestoreResult.SnapshotRejected;
            }

            try
            {
                GameDataManager.ReplacePlayerData(restoredData);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Existing account snapshot apply failed ({exception.GetType().Name}).");
                return ExistingAccountRestoreResult.ApplyFailed;
            }

            DiscardPendingForOwner(playerId);
            _conflictService.ClearConflict();
            SetCurrentCloudVersion(playerId, readResult);
            Debug.Log("[CloudSave] Existing account snapshot restored.");
            return ExistingAccountRestoreResult.Restored;
        }

        /// <summary>Отписывает сервис от источников checkpoint.</summary>
        public void Dispose()
        {
            _accountService.CurrentGuestLinked -= OnCurrentGuestLinked;
            _accountService.StateChanged -= OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted -= OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed -= OnApplicationResumed;
        }

        /// <summary>
        /// Отправляет текущий pending и после него сразу продолжает с самым новым снимком.
        /// </summary>
        private async Task UploadPendingSnapshotAsync(bool isRetry)
        {
            if (_isSnapshotUploadActive || CurrentConflict != null)
                return;

            var snapshot = _pendingSnapshot;
            if (snapshot == null)
                return;

            // Фиксируем active отдельно: новые checkpoint заменяют только pending.
            _pendingSnapshot = null;
            _isSnapshotUploadActive = true;
            var continueWithNewerSnapshot = false;

            try
            {
                // Перед записью классифицируем ветки по общей server revision.
                var cloudVersion = await _gateway.LoadSnapshotAsync();
                var localForDecision = _pendingSnapshot ?? snapshot;
                var shouldUpload = false;

                if (cloudVersion == null)
                {
                    if (string.IsNullOrWhiteSpace(snapshot.BaseRevision))
                    {
                        ResetMissingCloudRetry();
                        shouldUpload = true;
                    }
                    else if (IsRepeatedMissingCloud(localForDecision))
                    {
                        // Повторно подтверждённое отсутствие ключа разрешает безопасное recreate.
                        snapshot.BaseRevision = null;
                        if (_pendingSnapshot == null)
                            CloudPendingSnapshotStore.Save(snapshot);

                        ResetMissingCloudRetry();
                        shouldUpload = true;
                    }
                    else
                    {
                        RememberMissingCloud(localForDecision);
                        RetainActiveSnapshot(snapshot);
                        Debug.LogError("[CloudSave] Pending base is missing in cloud; retry required.");
                    }
                }
                else if (!string.Equals(
                             cloudVersion.Snapshot.PlayerId,
                             snapshot.PlayerId,
                             StringComparison.Ordinal))
                {
                    RetainActiveSnapshot(snapshot);
                    Debug.LogError("[CloudSave] Pending snapshot owner mismatch.");
                }
                else if (AreEquivalent(snapshot, cloudVersion.Snapshot))
                {
                    // Сервер уже содержит этот pending: предыдущий ack был потерян.
                    ResetMissingCloudRetry();
                    SetCurrentCloudVersion(snapshot.PlayerId, cloudVersion);
                    CompleteConfirmedSnapshot(snapshot, cloudVersion.ServerRevision);
                    continueWithNewerSnapshot = _pendingSnapshot != null;
                }
                else if (!string.Equals(
                             snapshot.BaseRevision,
                             cloudVersion.ServerRevision,
                             StringComparison.Ordinal))
                {
                    ResetMissingCloudRetry();
                    RetainActiveSnapshot(snapshot);
                    _conflictService.SetConflict(_pendingSnapshot, cloudVersion);
                }
                else
                {
                    ResetMissingCloudRetry();
                    shouldUpload = true;
                }

                if (shouldUpload)
                {
                    Debug.Log(isRetry
                        ? "[CloudSave] First snapshot retry started."
                        : "[CloudSave] First snapshot upload started.");

                    var result = await _gateway.SaveSnapshotAsync(snapshot)
                        ?? throw new InvalidOperationException("Cloud Save returned no write result.");
                    SetCurrentCloudVersion(snapshot.PlayerId, result);
                    CompleteConfirmedSnapshot(snapshot, result.ServerRevision);
                    continueWithNewerSnapshot = _pendingSnapshot != null;

                    Debug.Log(isRetry
                        ? "[CloudSave] First snapshot retry completed."
                        : "[CloudSave] First snapshot upload completed.");
                }
            }
            catch (Exception exception)
            {
                RetainActiveSnapshot(snapshot);
                Debug.LogError($"[CloudSave] First snapshot upload failed ({exception.GetType().Name}).");
            }
            finally
            {
                _isSnapshotUploadActive = false;
            }

            if (continueWithNewerSnapshot && CurrentConflict == null)
                await UploadPendingSnapshotAsync(isRetry: false);
        }

        /// <summary>Продолжает pending владельца или создаёт первый снимок нового владельца.</summary>
        private void OnCurrentGuestLinked(string playerId)
        {
            if (!_isSnapshotUploadActive &&
                _pendingSnapshot != null &&
                !string.Equals(_pendingSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                _pendingSnapshot = null;
            }

            _ = UploadFirstSnapshotAsync(playerId);
        }

        /// <summary>Запускает durable retry после определения связанного аккаунта.</summary>
        private void OnAccountStateChanged(AccountState state)
        {
            if (state == AccountState.Linked)
                TryUploadPendingForCurrentAccount();
        }

        /// <summary>Повторяет durable pending после возврата приложения.</summary>
        private void OnApplicationResumed()
        {
            TryUploadPendingForCurrentAccount();
        }

        /// <summary>Фиксирует успешный локальный checkpoint для связанного аккаунта.</summary>
        private void OnCheckpointCommitted(CheckpointReason reason)
        {
            // AccountLinked уже создаёт первый снимок в отдельном Task 02 flow.
            if (reason == CheckpointReason.AccountLinked ||
                !_accountService.TryGetLinkedPlayerId(out var playerId))
            {
                return;
            }

            // JSON payload отделяет снимок от последующих изменений PlayerData.
            var snapshot = CloudSaveSnapshotCodec.Capture(
                GameDataManager.PlayerData,
                playerId,
                GetNextLocalRevision(),
                CurrentCloudVersion?.ServerRevision);
            _pendingSnapshot = snapshot;
            CloudPendingSnapshotStore.Save(snapshot);

            if (CurrentConflict != null)
            {
                _conflictService.SetConflict(snapshot, CurrentConflict.CloudVersion);
            }
            else if (!_isSnapshotUploadActive)
            {
                _ = UploadPendingSnapshotAsync(isRetry: false);
            }
        }

        /// <summary>Отправляет durable pending только после определения его владельца.</summary>
        private void TryUploadPendingForCurrentAccount()
        {
            if (_isSnapshotUploadActive ||
                _conflictService.IsResolutionActive ||
                CurrentConflict != null ||
                !_accountService.TryGetLinkedPlayerId(out var playerId))
            {
                return;
            }

            RestoreCurrentCloudVersion(playerId);

            if (_pendingSnapshot != null)
            {
                if (string.Equals(_pendingSnapshot.PlayerId, playerId, StringComparison.Ordinal))
                    _ = UploadPendingSnapshotAsync(isRetry: true);

                return;
            }

            if (CurrentCloudVersion != null && !_isCloudRefreshActive)
                _ = RefreshCloudOnlyAsync(playerId);
        }

        /// <summary>Проверяет cloud-only lag после готовности аккаунта или resume.</summary>
        private async Task RefreshCloudOnlyAsync(string playerId)
        {
            _isCloudRefreshActive = true;
            var retryPending = false;
            try
            {
                var cloudVersion = await _gateway.LoadSnapshotAsync();
                if (_pendingSnapshot != null)
                {
                    retryPending = true;
                    return;
                }

                if (cloudVersion == null)
                {
                    var snapshot = CloudSaveSnapshotCodec.Capture(
                        GameDataManager.PlayerData,
                        playerId,
                        GetNextLocalRevision(),
                        CurrentCloudVersion.ServerRevision);
                    _pendingSnapshot = snapshot;
                    CloudPendingSnapshotStore.Save(snapshot);
                    RememberMissingCloud(snapshot);
                    Debug.LogError("[CloudSave] Confirmed cloud snapshot missing; local retry retained.");
                    return;
                }

                if (!string.Equals(cloudVersion.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    Debug.LogError("[CloudSave] Cloud-only refresh owner mismatch.");
                    return;
                }

                if (string.Equals(
                        cloudVersion.ServerRevision,
                        CurrentCloudVersion.ServerRevision,
                        StringComparison.Ordinal))
                {
                    return;
                }

                if (!CloudSaveConflictService.TryRestoreValidatedPlayerData(
                        cloudVersion.Snapshot,
                        out var restoredData,
                        out var rejectionReason))
                {
                    Debug.LogWarning($"[CloudSave] Cloud-only snapshot rejected ({rejectionReason}).");
                    return;
                }

                GameDataManager.ReplacePlayerData(restoredData);
                SetCurrentCloudVersion(playerId, cloudVersion);
                Debug.Log("[CloudSave] Cloud-only lag applied.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Cloud-only refresh failed ({exception.GetType().Name}).");
            }
            finally
            {
                _isCloudRefreshActive = false;
                if (retryPending && CurrentConflict == null)
                    _ = UploadPendingSnapshotAsync(isRetry: true);
            }
        }

        private void RestoreCurrentCloudVersion(string playerId)
        {
            if (string.Equals(
                    _currentCloudVersionPlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                return;
            }

            CurrentCloudVersion = CloudPendingSnapshotStore.LoadConfirmedVersion(playerId);
            _currentCloudVersionPlayerId = playerId;
        }

        private void SetCurrentCloudVersion(
            string playerId,
            CloudSaveReadResult version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            SetCurrentCloudVersion(
                playerId,
                new CloudSaveWriteResult(
                    version.ServerRevision,
                    version.ServerModifiedAtUtc));
        }

        private void SetCurrentCloudVersion(
            string playerId,
            CloudSaveWriteResult version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            CurrentCloudVersion = version;
            _currentCloudVersionPlayerId = playerId;
            CloudPendingSnapshotStore.SaveConfirmedVersion(playerId, version);
        }

        private void DiscardPendingForOwner(string playerId)
        {
            if (_pendingSnapshot != null &&
                string.Equals(_pendingSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                CloudPendingSnapshotStore.ClearIfMatches(_pendingSnapshot);
                _pendingSnapshot = null;
            }
            else
            {
                var durablePending = CloudPendingSnapshotStore.Load();
                if (durablePending != null &&
                    string.Equals(durablePending.PlayerId, playerId, StringComparison.Ordinal))
                {
                    CloudPendingSnapshotStore.ClearIfMatches(durablePending);
                }
            }

            if (_firstSnapshotAwaitingConfirmation != null &&
                string.Equals(
                    _firstSnapshotAwaitingConfirmation.PlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                _firstSnapshotAwaitingConfirmation = null;
            }

            ResetMissingCloudRetry();
        }

        private void CompleteConfirmedSnapshot(
            CloudSaveSnapshot snapshot,
            string serverRevision)
        {
            CloudPendingSnapshotStore.ClearIfMatches(snapshot);
            if (ReferenceEquals(_firstSnapshotAwaitingConfirmation, snapshot))
                _firstSnapshotAwaitingConfirmation = null;

            RebasePendingTo(serverRevision);
        }

        private void RebasePendingTo(string serverRevision)
        {
            if (_pendingSnapshot == null)
                return;

            _pendingSnapshot.BaseRevision = serverRevision;
            CloudPendingSnapshotStore.Save(_pendingSnapshot);
        }

        private void RetainActiveSnapshot(CloudSaveSnapshot snapshot)
        {
            if (_pendingSnapshot == null)
            {
                _pendingSnapshot = snapshot;
                return;
            }

            if (ReferenceEquals(_firstSnapshotAwaitingConfirmation, snapshot))
                _firstSnapshotAwaitingConfirmation = null;
        }

        private bool IsRepeatedMissingCloud(CloudSaveSnapshot snapshot)
        {
            return string.Equals(
                       _missingCloudPendingPlayerId,
                       snapshot.PlayerId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       _missingCloudPendingRevision,
                       snapshot.Revision,
                       StringComparison.Ordinal);
        }

        private void RememberMissingCloud(CloudSaveSnapshot snapshot)
        {
            _missingCloudPendingPlayerId = snapshot.PlayerId;
            _missingCloudPendingRevision = snapshot.Revision;
        }

        private void ResetMissingCloudRetry()
        {
            _missingCloudPendingPlayerId = null;
            _missingCloudPendingRevision = null;
        }

        private static bool AreEquivalent(
            CloudSaveSnapshot first,
            CloudSaveSnapshot second)
        {
            return string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal) &&
                   string.Equals(first.Revision, second.Revision, StringComparison.Ordinal) &&
                   string.Equals(first.BaseRevision, second.BaseRevision, StringComparison.Ordinal) &&
                   string.Equals(first.SavedAtUtc, second.SavedAtUtc, StringComparison.Ordinal) &&
                   string.Equals(first.PlayerDataJson, second.PlayerDataJson, StringComparison.Ordinal);
        }

        private static bool IsSamePending(
            CloudSaveSnapshot first,
            CloudSaveSnapshot second)
        {
            return first != null &&
                   second != null &&
                   string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal) &&
                   string.Equals(first.Revision, second.Revision, StringComparison.Ordinal);
        }

        /// <summary>Продолжает локальную revision после восстановленного durable pending.</summary>
        private void AdvanceLocalRevisionPast(CloudSaveSnapshot snapshot)
        {
            if (snapshot != null &&
                long.TryParse(snapshot.Revision, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) &&
                revision >= _nextLocalRevision)
            {
                _nextLocalRevision = revision + 1;
            }
        }

        /// <summary>Возвращает следующую локальную revision текущей сессии.</summary>
        private string GetNextLocalRevision()
        {
            return (_nextLocalRevision++).ToString(CultureInfo.InvariantCulture);
        }
    }
}
