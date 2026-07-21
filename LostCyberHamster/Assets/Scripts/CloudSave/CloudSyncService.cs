using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Создаёт первый облачный снимок после связывания текущего гостя.
    /// </summary>
    public sealed class CloudSyncService
    {
        private const string FirstLocalRevision = "1";

        private readonly ICloudSaveGateway _gateway;
        private CloudSaveSnapshot _pendingFirstSnapshot;
        private bool _isFirstSnapshotUploadActive;

        /// <summary>Подписывает синхронизацию на успешное связывание текущего гостя.</summary>
        public CloudSyncService(ICloudSaveGateway gateway, AccountService accountService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (accountService == null)
            {
                throw new ArgumentNullException(nameof(accountService));
            }

            accountService.CurrentGuestLinked += OnCurrentGuestLinked;
        }

        /// <summary>Последняя подтверждённая сервером версия текущего процесса игры.</summary>
        public CloudSaveWriteResult CurrentCloudVersion { get; private set; }

        /// <summary>Есть первый снимок, который облако ещё не подтвердило.</summary>
        public bool HasPendingFirstSnapshot => _pendingFirstSnapshot != null;

        /// <summary>
        /// Сначала сохраняет полный прогресс локально, затем отправляет его первый снимок в облако.
        /// </summary>
        public async Task UploadFirstSnapshotAsync(string playerId)
        {
            if (_isFirstSnapshotUploadActive ||
                _pendingFirstSnapshot != null ||
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
                    FirstLocalRevision);
                _pendingFirstSnapshot = snapshot;

                await UploadPendingFirstSnapshotAsync(isRetry: false);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] First snapshot upload failed ({exception.GetType().Name}).");
            }
        }

        /// <summary>Повторно отправляет тот же неподтверждённый снимок.</summary>
        public Task RetryPendingFirstSnapshotAsync()
        {
            if (_isFirstSnapshotUploadActive)
            {
                Debug.Log("[CloudSave] First snapshot retry skipped: upload active.");
                return Task.CompletedTask;
            }

            if (_pendingFirstSnapshot == null)
            {
                Debug.Log("[CloudSave] First snapshot retry skipped: no pending snapshot.");
                return Task.CompletedTask;
            }

            return UploadPendingFirstSnapshotAsync(isRetry: true);
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

        private async Task UploadPendingFirstSnapshotAsync(bool isRetry)
        {
            if (_isFirstSnapshotUploadActive)
                return;

            var snapshot = _pendingFirstSnapshot;
            if (snapshot == null)
                return;

            _isFirstSnapshotUploadActive = true;

            try
            {
                Debug.Log(isRetry
                    ? "[CloudSave] First snapshot retry started."
                    : "[CloudSave] First snapshot upload started.");

                var result = await _gateway.SaveSnapshotAsync(snapshot);
                CurrentCloudVersion = result
                    ?? throw new InvalidOperationException("Cloud Save returned no write result.");

                if (ReferenceEquals(_pendingFirstSnapshot, snapshot))
                    _pendingFirstSnapshot = null;

                Debug.Log(isRetry
                    ? "[CloudSave] First snapshot retry completed."
                    : "[CloudSave] First snapshot upload completed.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] First snapshot upload failed ({exception.GetType().Name}).");
            }
            finally
            {
                _isFirstSnapshotUploadActive = false;
            }
        }

        private void OnCurrentGuestLinked(string playerId)
        {
            _ = UploadFirstSnapshotAsync(playerId);
        }
    }
}
