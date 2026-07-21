using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит конфликт и выполняет выбор одной целой cloud/local ветки.</summary>
    public sealed class CloudSaveConflictService
    {
        private readonly ICloudSaveGateway _gateway;
        private readonly AccountService _accountService;
        private bool _isConflictResolutionActive;

        public CloudSaveConflictService(
            ICloudSaveGateway gateway,
            AccountService accountService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        /// <summary>Текущие две независимо изменённые ветки, ожидающие выбора.</summary>
        public CloudSaveConflictModel CurrentConflict { get; private set; }

        /// <summary>Возникает при обнаружении или обновлении данных конфликта.</summary>
        public event Action<CloudSaveConflictModel> ConflictDetected;

        internal bool IsResolutionActive => _isConflictResolutionActive;

        /// <summary>Проверяет актуальность выбранного cloud snapshot и целиком применяет его локально.</summary>
        public async Task<CloudSaveReadResult> ResolveWithCloudAsync()
        {
            var conflict = CurrentConflict;
            if (_isConflictResolutionActive ||
                conflict == null ||
                !_accountService.TryGetLinkedPlayerId(out var playerId) ||
                !string.Equals(conflict.LocalSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                return null;
            }

            _isConflictResolutionActive = true;
            try
            {
                var latestCloud = await _gateway.LoadSnapshotAsync();
                if (latestCloud == null ||
                    !string.Equals(latestCloud.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    Debug.LogError("[CloudSave] Cloud conflict choice failed: current cloud unavailable.");
                    return null;
                }

                if (!ReferenceEquals(CurrentConflict, conflict) ||
                    !string.Equals(
                        latestCloud.ServerRevision,
                        conflict.CloudVersion.ServerRevision,
                        StringComparison.Ordinal))
                {
                    SetConflict(CurrentConflict?.LocalSnapshot ?? conflict.LocalSnapshot, latestCloud);
                    return null;
                }

                if (!TryRestoreValidatedPlayerData(
                        latestCloud.Snapshot,
                        out var restoredData,
                        out var rejectionReason))
                {
                    Debug.LogWarning($"[CloudSave] Conflict cloud snapshot rejected ({rejectionReason}).");
                    return null;
                }

                try
                {
                    GameDataManager.ReplacePlayerData(restoredData);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[CloudSave] Conflict cloud apply failed ({exception.GetType().Name}).");
                    return null;
                }

                ClearConflict();
                Debug.Log("[CloudSave] Conflict resolved with cloud snapshot.");
                return latestCloud;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Cloud conflict choice failed ({exception.GetType().Name}).");
                return null;
            }
            finally
            {
                _isConflictResolutionActive = false;
            }
        }

        /// <summary>Записывает выбранный local snapshot целиком поверх актуальной cloud revision.</summary>
        public async Task<CloudSaveWriteResult> ResolveWithLocalAsync()
        {
            var conflict = CurrentConflict;
            if (_isConflictResolutionActive ||
                conflict == null ||
                !_accountService.TryGetLinkedPlayerId(out var playerId) ||
                !string.Equals(conflict.LocalSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                return null;
            }

            _isConflictResolutionActive = true;
            CloudSaveReadResult latestCloud = null;
            try
            {
                latestCloud = await _gateway.LoadSnapshotAsync();
                if (latestCloud == null ||
                    !string.Equals(latestCloud.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    Debug.LogError("[CloudSave] Local conflict choice failed: current cloud unavailable.");
                    return null;
                }

                if (!ReferenceEquals(CurrentConflict, conflict))
                    return null;

                conflict.LocalSnapshot.BaseRevision = latestCloud.ServerRevision;
                CloudPendingSnapshotStore.Save(conflict.LocalSnapshot);

                var result = await _gateway.SaveSnapshotAsync(conflict.LocalSnapshot)
                    ?? throw new InvalidOperationException("Cloud Save returned no write result.");
                ClearConflict();
                Debug.Log("[CloudSave] Conflict resolved with local snapshot.");
                return result;
            }
            catch (Exception exception)
            {
                if (latestCloud != null)
                {
                    SetConflict(
                        CurrentConflict?.LocalSnapshot ?? conflict.LocalSnapshot,
                        latestCloud);
                }

                Debug.LogError($"[CloudSave] Local conflict choice failed ({exception.GetType().Name}).");
                return null;
            }
            finally
            {
                _isConflictResolutionActive = false;
            }
        }

        internal void SetConflict(
            CloudSaveSnapshot localSnapshot,
            CloudSaveReadResult cloudVersion)
        {
            CurrentConflict = new CloudSaveConflictModel(localSnapshot, cloudVersion);
            var handlers = ConflictDetected;
            if (handlers == null)
                return;

            foreach (Action<CloudSaveConflictModel> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(CurrentConflict);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[CloudSave] Conflict subscriber failed ({exception.GetType().Name}).");
                }
            }
        }

        internal void ClearConflict()
        {
            CurrentConflict = null;
        }

        internal static bool TryRestoreValidatedPlayerData(
            CloudSaveSnapshot snapshot,
            out PlayerData restoredData,
            out string rejectionReason)
        {
            restoredData = null;
            rejectionReason = string.Empty;
            try
            {
                var candidate = CloudSaveSnapshotCodec.RestorePlayerData(snapshot);
                var validation = PlayerDataValidator.Validate(candidate);
                if (validation.Status == PlayerDataValidationStatus.Repairable)
                {
                    PlayerDataValidator.RepairSafe(candidate, validation);
                    validation = PlayerDataValidator.Validate(candidate);
                }

                if (validation.Status != PlayerDataValidationStatus.Valid)
                {
                    rejectionReason = validation.Reason;
                    return false;
                }

                restoredData = candidate;
                return true;
            }
            catch (Exception exception)
            {
                rejectionReason = exception.GetType().Name;
                return false;
            }
        }
    }
}
