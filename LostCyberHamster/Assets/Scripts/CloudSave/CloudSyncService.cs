using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Последовательно синхронизирует локальный и облачный прогресс.</summary>
    public sealed class CloudSyncService : IDisposable
    {
        /// <summary>Предоставляет текущий аккаунт.</summary>
        private readonly AccountService _accountService;

        /// <summary>Хранит подтверждённые облачные версии.</summary>
        private readonly ICloudSaveVersionStore _versionStore;

        /// <summary>Читает и записывает облачный снимок.</summary>
        private readonly ICloudSaveGateway _gateway;

        /// <summary>Управляет локальным снимком.</summary>
        private readonly SnapshotService _snapshotService;

        /// <summary>Управляет конфликтом прогресса.</summary>
        private readonly ConflictService _conflictService;

        /// <summary>Не допускает параллельные отправки.</summary>
        private bool _isUploadActive;

        /// <summary>Не допускает параллельные циклы синхронизации.</summary>
        private bool _isSynchronizationActive;

        /// <summary>Запоминает повторный запрос, полученный во время синхронизации.</summary>
        private bool _isSynchronizationRequested;

        /// <summary>Инвалидирует ответы, начатые до смены состояния аккаунта.</summary>
        private int _accountLifecycleVersion;

        /// <summary>Запрещает обработку новых событий.</summary>
        private bool _isDisposed;

        public CloudSyncService(
            AccountService accountService,
            ICloudSaveVersionStore versionStore,
            ICloudSaveGateway gateway,
            SnapshotService snapshotService,
            ConflictService conflictService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));

            _accountService.CurrentGuestLinked += OnAccountLinked;
            _accountService.StateChanged += OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed += OnApplicationResumed;
            _conflictService.ConflictResolved += OnConflictResolved;
        }

        /// <summary>Текущее состояние облачной синхронизации.</summary>
        public CloudSyncStatusEnum Status
        {
            get
            {
                if (_conflictService.CurrentConflict != null)
                    return CloudSyncStatusEnum.Conflict;
                if (_isSynchronizationActive || _isUploadActive)
                    return CloudSyncStatusEnum.Synchronizing;
                if (_snapshotService.Snapshot != null)
                    return CloudSyncStatusEnum.Pending;

                return CloudSyncStatusEnum.Saved;
            }
        }

        /// <summary>Возникает при изменении состояния синхронизации.</summary>
        public event Action<CloudSyncStatusEnum> StatusChanged;

        #region Обработка событий

        /// <summary>Запрашивает синхронизацию после привязки гостя.</summary>
        private void OnAccountLinked(string _)
        {
            RequestSynchronization();
        }

        /// <summary>Инвалидирует старые ответы и запускает синхронизацию связанного аккаунта.</summary>
        private void OnAccountStateChanged(AccountState state)
        {
            if (_isDisposed)
                return;

            _accountLifecycleVersion++;

            if (state == AccountState.Linked)
                RequestSynchronization();
            else
            {
                _conflictService.ClearConflict();
                NotifyStatusChanged();
            }
        }

        /// <summary>Ставит новый локальный checkpoint в очередь синхронизации.</summary>
        private void OnCheckpointCommitted(CheckpointReason reason)
        {
            if (_isDisposed || reason == CheckpointReason.AccountLinked)
                return;

            if (reason == CheckpointReason.MenuEntered)
            {
                RequestSynchronization();
                return;
            }

            CreateCloudSave();
        }

        /// <summary>Повторяет синхронизацию после возврата в игру.</summary>
        private void OnApplicationResumed()
        {
            RequestSynchronization();
        }

        /// <summary>Продолжает синхронизацию после разрешения конфликта.</summary>
        private void OnConflictResolved()
        {
            NotifyStatusChanged();
            RequestSynchronization();
        }

        #endregion

        #region Основные методы

        /// <summary>Создаёт первое облачное сохранение внутри текущего account lifecycle.</summary>
        private async Task CreateFirstCloudSaveAsync(
            string playerId,
            int lifecycleVersion,
            bool allowSigningIn)
        {
            if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn))
                return;

            // Готовим первый снимок, если очередь владельца пуста.
            if (_snapshotService.Snapshot == null)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AccountLinked);
                if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn))
                    return;

                _snapshotService.SetPending(new CloudSaveSnapshot(
                    playerId,
                    GameDataManager.PlayerData.ToJson()));
                NotifyStatusChanged();
            }

            // Не отправляем pending другого владельца через текущую identity.
            if (!IsPendingOwnedBy(playerId))
            {
                Debug.LogError("[CloudSave] First snapshot upload rejected: pending owner mismatch.");
                return;
            }

            await UploadPendingSnapshotAsync(
                null,
                playerId,
                lifecycleVersion,
                allowSigningIn);
            if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn))
                return;
        }

        /// <summary>Фиксирует последний локальный прогресс как pending.</summary>
        private void CreateCloudSave()
        {
            if (!_accountService.TryGetLinkedPlayerId(out var playerId))
                return;

            // Не теряем pending предыдущего владельца и не отправляем его через новый аккаунт.
            if (_snapshotService.Snapshot != null && !IsPendingOwnedBy(playerId))
            {
                Debug.LogError("[CloudSave] Checkpoint retained locally: pending belongs to another player.");
                return;
            }

            var snapshot = new CloudSaveSnapshot(
                playerId,
                GameDataManager.PlayerData.ToJson());
            _snapshotService.SetPending(snapshot);

            // Открытый конфликт всегда показывает последний durable checkpoint.
            if (_conflictService.TryUpdateLocalSnapshot(snapshot))
            {
                NotifyStatusChanged();
                return;
            }

            NotifyStatusChanged();

            RequestSynchronization();
        }

        /// <summary>Восстанавливает прогресс существующего аккаунта.</summary>
        public async Task RestoreProgressAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));

            var lifecycleVersion = _accountLifecycleVersion;
            if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn: true))
                throw new InvalidOperationException("Existing account restore is not current.");

            var pendingBeforeRestore = _snapshotService.Snapshot;

            // Загружаем снимок и отбрасываем ответ прошлого account lifecycle.
            var cloudSave = await _gateway.LoadSnapshotAsync();
            EnsureOperationCurrent(playerId, lifecycleVersion, allowSigningIn: true);

            if (cloudSave == null)
                throw new InvalidOperationException("Existing account cloud snapshot is missing.");

            EnsureCloudOwner(playerId, cloudSave);

            // Применяем только актуальный снимок выбранного владельца.
            EnsureOperationCurrent(playerId, lifecycleVersion, allowSigningIn: true);
            ApplyCloudProgress(playerId, cloudSave);
            EnsureOperationCurrent(playerId, lifecycleVersion, allowSigningIn: true);
            if (pendingBeforeRestore != null &&
                string.Equals(
                    pendingBeforeRestore.PlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                _snapshotService.ClearIfCurrent(pendingBeforeRestore);
            }
            NotifyStatusChanged();
        }

        /// <summary>Разрешает текущий конфликт актуальной облачной веткой.</summary>
        public Task<bool> ResolveConflictWithCloudAsync()
        {
            if (_isDisposed ||
                !_accountService.TryGetLinkedPlayerId(out var playerId))
            {
                return Task.FromResult(false);
            }

            var lifecycleVersion = _accountLifecycleVersion;
            return _conflictService.ResolveWithCloudAsync(
                playerId,
                () => IsOperationCurrent(
                    playerId,
                    lifecycleVersion,
                    allowSigningIn: false));
        }

        /// <summary>Разрешает текущий конфликт актуальной локальной веткой.</summary>
        public Task<bool> ResolveConflictWithLocalAsync()
        {
            if (_isDisposed ||
                !_accountService.TryGetLinkedPlayerId(out var playerId))
            {
                return Task.FromResult(false);
            }

            var lifecycleVersion = _accountLifecycleVersion;
            return _conflictService.ResolveWithLocalAsync(
                playerId,
                () => IsOperationCurrent(
                    playerId,
                    lifecycleVersion,
                    allowSigningIn: false));
        }

        /// <summary>Запоминает запрос и запускает единственный последовательный sync pump.</summary>
        private void RequestSynchronization()
        {
            if (_isDisposed)
                return;

            _isSynchronizationRequested = true;
            if (!_isSynchronizationActive)
                _ = RunSynchronizationPumpAsync();
        }

        /// <summary>Последовательно обрабатывает текущий и все повторные запросы.</summary>
        private async Task RunSynchronizationPumpAsync()
        {
            if (_isSynchronizationActive || _isDisposed)
                return;

            _isSynchronizationActive = true;
            NotifyStatusChanged();

            try
            {
                while (!_isDisposed && _isSynchronizationRequested)
                {
                    _isSynchronizationRequested = false;
                    if (_conflictService.CurrentConflict != null ||
                        !_accountService.TryGetLinkedPlayerId(out var playerId))
                    {
                        break;
                    }

                    var lifecycleVersion = _accountLifecycleVersion;
                    try
                    {
                        await SynchronizeProgressOnceAsync(playerId, lifecycleVersion);
                        if (!IsOperationCurrent(
                                playerId,
                                lifecycleVersion,
                                allowSigningIn: false))
                        {
                            continue;
                        }
                    }
                    catch (Exception exception)
                    {
                        if (IsOperationCurrent(
                                playerId,
                                lifecycleVersion,
                                allowSigningIn: false))
                        {
                            Debug.LogError(
                                $"[CloudSave] Synchronization failed ({exception.GetType().Name}).");
                        }
                    }
                }
            }
            finally
            {
                _isSynchronizationActive = false;
                NotifyStatusChanged();

                // Событие на границе завершения не должно потерять повторный запуск.
                if (_isSynchronizationRequested && !_isDisposed)
                    RequestSynchronization();
            }
        }

        /// <summary>Выполняет одну согласованную проверку cloud state.</summary>
        private async Task SynchronizeProgressOnceAsync(
            string playerId,
            int lifecycleVersion)
        {
            var confirmedRevision = _versionStore.GetConfirmedRevision(playerId);
            var cloudSave = await _gateway.LoadSnapshotAsync();

            // Не классифицируем cloud ответ после смены аккаунта или локальной base revision.
            if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn: false))
                return;
            if (!string.Equals(
                    confirmedRevision,
                    _versionStore.GetConfirmedRevision(playerId),
                    StringComparison.Ordinal))
            {
                _isSynchronizationRequested = true;
                return;
            }

            if (cloudSave != null)
                EnsureCloudOwner(playerId, cloudSave);

            var pendingSnapshot = _snapshotService.Snapshot;
            if (pendingSnapshot != null && !IsPendingOwnedBy(playerId))
            {
                Debug.LogError("[CloudSave] Synchronization stopped: pending owner mismatch.");
                return;
            }

            // Потерянный ответ upload не должен создавать ложный конфликт.
            if (cloudSave != null &&
                pendingSnapshot != null &&
                AreSnapshotsEquivalent(pendingSnapshot, cloudSave.Snapshot))
            {
                _versionStore.SaveConfirmedVersion(
                    playerId,
                    cloudSave.Version.ServerRevision);
                _snapshotService.ClearIfCurrent(pendingSnapshot);
                return;
            }

            var syncState = GetSyncState(
                cloudSave,
                pendingSnapshot,
                confirmedRevision);
            switch (syncState)
            {
                case CloudSyncStateEnum.CloudMissing:
                    await CreateFirstCloudSaveAsync(
                        playerId,
                        lifecycleVersion,
                        allowSigningIn: false);
                    if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn: false))
                        return;
                    break;

                case CloudSyncStateEnum.LocalChanged:
                    await UploadPendingSnapshotAsync(
                        cloudSave.Version.ServerRevision,
                        playerId,
                        lifecycleVersion,
                        allowSigningIn: false);
                    if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn: false))
                        return;
                    break;

                case CloudSyncStateEnum.CloudChanged:
                    if (!CanApplyCloudProgress(playerId, lifecycleVersion, confirmedRevision))
                    {
                        _isSynchronizationRequested = true;
                        return;
                    }

                    ApplyCloudProgress(playerId, cloudSave);
                    break;

                case CloudSyncStateEnum.Conflict:
                    _conflictService.SetConflict(pendingSnapshot, cloudSave);
                    NotifyStatusChanged();
                    break;

                case CloudSyncStateEnum.Synchronized:
                    break;
            }
        }

        /// <summary>Последовательно отправляет active и самый новый pending.</summary>
        private async Task UploadPendingSnapshotAsync(
            string expectedRevision,
            string playerId,
            int lifecycleVersion,
            bool allowSigningIn)
        {
            if (_isUploadActive || _snapshotService.Snapshot == null)
                return;

            _isUploadActive = true;
            NotifyStatusChanged();

            try
            {
                while (_snapshotService.Snapshot != null)
                {
                    if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn))
                        return;

                    var snapshot = _snapshotService.Snapshot;
                    if (!string.Equals(snapshot.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        Debug.LogError("[CloudSave] Snapshot upload stopped: pending owner mismatch.");
                        return;
                    }

                    var version = await _gateway.SaveSnapshotAsync(snapshot, expectedRevision);
                    if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn))
                    {
                        Debug.LogWarning(
                            "[CloudSave] Snapshot acknowledgement ignored: account lifecycle changed.");
                        return;
                    }

                    // Подтверждаем и очищаем только фактически отправленный объект.
                    _versionStore.SaveConfirmedVersion(snapshot.PlayerId, version.ServerRevision);
                    _snapshotService.ClearIfCurrent(snapshot);
                    expectedRevision = version.ServerRevision;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Snapshot upload failed ({exception.GetType().Name}).");
                throw;
            }
            finally
            {
                _isUploadActive = false;
                NotifyStatusChanged();
            }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>Определяет текущую ситуацию синхронизации.</summary>
        private static CloudSyncStateEnum GetSyncState(
            CloudSaveReadResult cloudSave,
            CloudSaveSnapshot pendingSnapshot,
            string confirmedRevision)
        {
            var hasCloudSave = cloudSave != null;
            var hasPending = pendingSnapshot != null;
            var cloudChanged = hasCloudSave &&
                !string.Equals(
                    confirmedRevision,
                    cloudSave.Version.ServerRevision,
                    StringComparison.Ordinal);

            return (hasCloudSave, hasPending, cloudChanged) switch
            {
                (false, _, _) => CloudSyncStateEnum.CloudMissing,
                (true, false, false) => CloudSyncStateEnum.Synchronized,
                (true, true, false) => CloudSyncStateEnum.LocalChanged,
                (true, false, true) => CloudSyncStateEnum.CloudChanged,
                (true, true, true) => CloudSyncStateEnum.Conflict
            };
        }

        /// <summary>Применяет облачный прогресс и подтверждает его версию.</summary>
        private void ApplyCloudProgress(
            string playerId,
            CloudSaveReadResult cloudSave)
        {
            var restoredData = PlayerData.FromJson(cloudSave.Snapshot.PlayerDataJson);
            GameDataManager.ReplacePlayerData(restoredData);
            _versionStore.SaveConfirmedVersion(
                playerId,
                cloudSave.Version.ServerRevision);
        }

        /// <summary>Проверяет, можно ли применить результат cloud-only чтения.</summary>
        private bool CanApplyCloudProgress(
            string playerId,
            int lifecycleVersion,
            string startingConfirmedRevision)
        {
            return IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn: false) &&
                   _conflictService.CurrentConflict == null &&
                   _snapshotService.Snapshot == null &&
                   string.Equals(
                       startingConfirmedRevision,
                       _versionStore.GetConfirmedRevision(playerId),
                       StringComparison.Ordinal);
        }

        /// <summary>Проверяет владельца текущего pending.</summary>
        private bool IsPendingOwnedBy(string playerId)
        {
            return _snapshotService.Snapshot != null &&
                   string.Equals(
                       _snapshotService.Snapshot.PlayerId,
                       playerId,
                       StringComparison.Ordinal);
        }

        /// <summary>Сравнивает точные снимки для восстановления потерянного acknowledgement.</summary>
        private static bool AreSnapshotsEquivalent(
            CloudSaveSnapshot first,
            CloudSaveSnapshot second)
        {
            return first != null &&
                   second != null &&
                   string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal) &&
                   string.Equals(
                       first.PlayerDataJson,
                       second.PlayerDataJson,
                       StringComparison.Ordinal) &&
                   first.SavedAtUtc == second.SavedAtUtc;
        }

        /// <summary>Проверяет lifecycle и владельца normal/restore операции.</summary>
        private bool IsOperationCurrent(
            string playerId,
            int lifecycleVersion,
            bool allowSigningIn)
        {
            if (_isDisposed || lifecycleVersion != _accountLifecycleVersion)
                return false;

            return _accountService.IsCurrentPlayer(playerId, allowSigningIn);
        }

        /// <summary>Прерывает restore после смены account lifecycle.</summary>
        private void EnsureOperationCurrent(
            string playerId,
            int lifecycleVersion,
            bool allowSigningIn)
        {
            if (!IsOperationCurrent(playerId, lifecycleVersion, allowSigningIn))
                throw new InvalidOperationException("Cloud operation belongs to a stale account lifecycle.");
        }

        /// <summary>Проверяет владельца загруженного облачного снимка.</summary>
        private static void EnsureCloudOwner(
            string playerId,
            CloudSaveReadResult cloudSave)
        {
            if (!string.Equals(
                    cloudSave.Snapshot.PlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cloud snapshot owner mismatch.");
            }
        }

        /// <summary>Безопасно сообщает подписчикам актуальный статус.</summary>
        private void NotifyStatusChanged()
        {
            var handlers = StatusChanged;
            if (handlers == null)
                return;

            foreach (Action<CloudSyncStatusEnum> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(Status);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[CloudSave] Status subscriber failed ({exception.GetType().Name}).");
                }
            }
        }

        #endregion

        /// <summary>Останавливает сервис и убирает подписки.</summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _accountLifecycleVersion++;
            _isSynchronizationRequested = false;
            _accountService.CurrentGuestLinked -= OnAccountLinked;
            _accountService.StateChanged -= OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted -= OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed -= OnApplicationResumed;
            _conflictService.ConflictResolved -= OnConflictResolved;
        }
    }
}
