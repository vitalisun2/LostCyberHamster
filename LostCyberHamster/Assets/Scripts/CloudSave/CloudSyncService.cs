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
        private CloudSaveSnapshot _pendingSnapshot;
        private CloudSaveSnapshot _firstSnapshotAwaitingConfirmation;
        private bool _isSnapshotUploadActive;
        private long _nextLocalRevision = 1;

        /// <summary>Подписывает синхронизацию на успешное связывание текущего гостя.</summary>
        public CloudSyncService(ICloudSaveGateway gateway, AccountService accountService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (accountService == null)
            {
                throw new ArgumentNullException(nameof(accountService));
            }

            _accountService = accountService;
            accountService.CurrentGuestLinked += OnCurrentGuestLinked;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
        }

        /// <summary>Последняя подтверждённая сервером версия текущего процесса игры.</summary>
        public CloudSaveWriteResult CurrentCloudVersion { get; private set; }

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

            PlayerData restoredData;
            try
            {
                restoredData = CloudSaveSnapshotCodec.RestorePlayerData(readResult.Snapshot);
                var validation = PlayerDataValidator.Validate(restoredData);
                if (validation.Status == PlayerDataValidationStatus.Repairable)
                {
                    PlayerDataValidator.RepairSafe(restoredData, validation);
                    validation = PlayerDataValidator.Validate(restoredData);
                }

                if (validation.Status != PlayerDataValidationStatus.Valid)
                {
                    Debug.LogWarning($"[CloudSave] Existing account snapshot rejected ({validation.Reason}).");
                    return ExistingAccountRestoreResult.SnapshotRejected;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CloudSave] Existing account snapshot rejected ({exception.GetType().Name}).");
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

            CurrentCloudVersion = new CloudSaveWriteResult(
                readResult.ServerRevision,
                readResult.ServerModifiedAtUtc);
            Debug.Log("[CloudSave] Existing account snapshot restored.");
            return ExistingAccountRestoreResult.Restored;
        }

        /// <summary>Отписывает сервис от источников checkpoint.</summary>
        public void Dispose()
        {
            _accountService.CurrentGuestLinked -= OnCurrentGuestLinked;
            PlayerProgressCommitter.CommitCompleted -= OnCheckpointCommitted;
        }

        /// <summary>
        /// Отправляет текущий pending и после него сразу продолжает с самым новым снимком.
        /// </summary>
        private async Task UploadPendingSnapshotAsync(bool isRetry)
        {
            if (_isSnapshotUploadActive)
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
                Debug.Log(isRetry
                    ? "[CloudSave] First snapshot retry started."
                    : "[CloudSave] First snapshot upload started.");

                var result = await _gateway.SaveSnapshotAsync(snapshot);
                CurrentCloudVersion = result
                    ?? throw new InvalidOperationException("Cloud Save returned no write result.");

                if (ReferenceEquals(_firstSnapshotAwaitingConfirmation, snapshot))
                    _firstSnapshotAwaitingConfirmation = null;

                continueWithNewerSnapshot = _pendingSnapshot != null;

                Debug.Log(isRetry
                    ? "[CloudSave] First snapshot retry completed."
                    : "[CloudSave] First snapshot upload completed.");
            }
            catch (Exception exception)
            {
                // Новый pending важнее неудачного active; иначе сохраняем active для ручного retry.
                continueWithNewerSnapshot = _pendingSnapshot != null;
                if (!continueWithNewerSnapshot)
                {
                    _pendingSnapshot = snapshot;
                }
                else if (ReferenceEquals(_firstSnapshotAwaitingConfirmation, snapshot))
                {
                    _firstSnapshotAwaitingConfirmation = null;
                }

                Debug.LogError($"[CloudSave] First snapshot upload failed ({exception.GetType().Name}).");
            }
            finally
            {
                _isSnapshotUploadActive = false;
            }

            if (continueWithNewerSnapshot)
                await UploadPendingSnapshotAsync(isRetry: false);
        }

        private void OnCurrentGuestLinked(string playerId)
        {
            _ = UploadFirstSnapshotAsync(playerId);
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

            if (!_isSnapshotUploadActive)
                _ = UploadPendingSnapshotAsync(isRetry: false);
        }

        private string GetNextLocalRevision()
        {
            return (_nextLocalRevision++).ToString(CultureInfo.InvariantCulture);
        }
    }
}
