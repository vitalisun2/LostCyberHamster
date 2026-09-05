using System;
using System.Threading.Tasks;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит две ветки и применяет только актуальный явный выбор игрока.</summary>
    public sealed class ConflictService
    {
        private const string JournalFeature = "cloud-conflict";
        private readonly ICloudSaveGateway _gateway;
        private bool _isResolutionActive;

        public ConflictService(ICloudSaveGateway gateway, ICloudSaveVersionStore versionStore, SnapshotService snapshotService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public CloudSaveConflict CurrentConflict { get; private set; }
        public string LastResolutionError { get; private set; }
        public bool IsResolutionActive => _isResolutionActive;
        public event Action<CloudSaveConflict> ConflictDetected;
        public event Action ConflictResolved;

        /// <summary>Пустая cloud ветка допускается только как явное принятие локального legacy.</summary>
        public void SetConflict(CloudSaveSnapshot localSnapshot, CloudSaveReadResult cloudSave)
        {
            if (localSnapshot == null) throw new ArgumentNullException(nameof(localSnapshot));
            if (cloudSave != null && localSnapshot.PlayerId != cloudSave.Snapshot.PlayerId)
                throw new InvalidOperationException("Conflict snapshot owner mismatch.");
            var record = new CloudConflictRecord
            {
                ProfileId = GameDataManager.ProfileId,
                PlayerId = localSnapshot.PlayerId,
                LocalSnapshotJson = localSnapshot.ToJson(),
                CloudSnapshotJson = cloudSave?.Snapshot.ToJson(),
                CloudRevision = cloudSave?.Version.ServerRevision
            };
            GameDataManager.ExecuteTechnicalTransaction(() =>
            {
                GameDataManager.SetJournalJson(JournalFeature, JsonUtility.ToJson(record), localSnapshot.PlayerId);
                GameDataManager.SetActiveConflictOwner(localSnapshot.PlayerId);
            });
            CurrentConflict = new CloudSaveConflict(localSnapshot, cloudSave);
            NotifyConflictDetected();
        }

        /// <summary>Восстанавливает отложенные ветки при возврате того же аккаунта.</summary>
        public void RestoreForOwner(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) { HidePresentation(); return; }
            var json = GameDataManager.GetJournalJson(JournalFeature, playerId);
            if (string.IsNullOrWhiteSpace(json)) { HidePresentation(); return; }
            var record = JsonUtility.FromJson<CloudConflictRecord>(json);
            if (record == null || record.PlayerId != playerId || record.ProfileId != GameDataManager.ProfileId)
            {
                HidePresentation();
                return;
            }
            var cloud = string.IsNullOrWhiteSpace(record.CloudSnapshotJson) ? null : new CloudSaveReadResult(
                CloudSaveSnapshot.FromJson(record.CloudSnapshotJson), new CloudSaveVersion(record.CloudRevision));
            CurrentConflict = new CloudSaveConflict(
                new CloudSaveSnapshot(playerId, GameDataManager.GetSavedPlayerDataJson()), cloud);
            NotifyConflictDetected();
        }

        /// <summary>Убирает UI чужого аккаунта, сохраняя его durable журнал.</summary>
        private void HidePresentation()
        {
            if (CurrentConflict == null) return;
            CurrentConflict = null;
            NotifyConflictResolved();
        }

        public bool TryUpdateLocalSnapshot(CloudSaveSnapshot localSnapshot)
        {
            if (localSnapshot == null) throw new ArgumentNullException(nameof(localSnapshot));
            if (CurrentConflict == null || CurrentConflict.LocalSnapshot.PlayerId != localSnapshot.PlayerId) return false;
            SetConflict(localSnapshot, CurrentConflict.CloudSave);
            return true;
        }

        public Task<bool> ResolveWithCloudAsync(string playerId, Func<bool> isOperationCurrent) =>
            ResolveAsync(playerId, isOperationCurrent, useCloud: true);

        public Task<bool> ResolveWithLocalAsync(string playerId, Func<bool> isOperationCurrent) =>
            ResolveAsync(playerId, isOperationCurrent, useCloud: false);

        private async Task<bool> ResolveAsync(string playerId, Func<bool> isOperationCurrent, bool useCloud)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.", nameof(playerId));
            if (isOperationCurrent == null) throw new ArgumentNullException(nameof(isOperationCurrent));
            var selected = CurrentConflict;
            if (_isResolutionActive || selected == null || selected.LocalSnapshot.PlayerId != playerId ||
                !isOperationCurrent() || !GameDataManager.CanApplyCloudProgress) return false;
            _isResolutionActive = true;
            LastResolutionError = null;
            try
            {
                // Повторное чтение защищает обе кнопки от незаметной смены показанной cloud ветки.
                var latestCloud = await _gateway.LoadSnapshotAsync();
                if (!isOperationCurrent()) return false;
                if (latestCloud != null && latestCloud.Snapshot.PlayerId != playerId)
                    throw new InvalidOperationException("Cloud snapshot owner mismatch.");
                if (GameDataManager.Generation != selected.ProfileGeneration ||
                    GameDataManager.LocalRevision != selected.LocalRevision ||
                    (latestCloud?.Version.ServerRevision ?? "missing") != selected.CloudRevision)
                {
                    SetConflict(new CloudSaveSnapshot(playerId, GameDataManager.GetSavedPlayerDataJson()), latestCloud);
                    LastResolutionError = "cloud_save_conflict_changed";
                    return false;
                }
                if (!GameDataManager.CanApplyCloudProgress) return false;

                if (useCloud)
                {
                    if (latestCloud == null)
                    {
                        LastResolutionError = "cloud_save_conflict_cloud_unavailable";
                        return false;
                    }
                    // Owner journals остаются на устройстве; receipts берём целиком из выбранного baseline.
                    GameDataManager.ApplyCloudPlayerData(PlayerData.FromJson(latestCloud.Snapshot.PlayerDataJson),
                        playerId, latestCloud.Version.ServerRevision);
                }
                else
                {
                    if (GameDataManager.OwnerPlayerId == null)
                        GameDataManager.BindOwner(playerId, allowLegacyAdoption: true);
                    if (GameDataManager.OwnerPlayerId != playerId)
                        throw new InvalidOperationException("Local snapshot belongs to another account.");
                    var generation = GameDataManager.Generation;
                    var attempt = new CloudUploadAttempt
                    {
                        ProfileId = GameDataManager.ProfileId,
                        OwnerPlayerId = playerId,
                        LocalRevision = selected.LocalRevision,
                        PayloadHash = CloudSaveSnapshot.ComputePayloadHash(selected.LocalSnapshot.PlayerDataJson),
                        ExpectedCloudRevision = latestCloud?.Version.ServerRevision
                    };
                    GameDataManager.RecordCloudUploadAttempt(attempt);
                    var acknowledgement = await _gateway.SaveSnapshotAsync(selected.LocalSnapshot, attempt.ExpectedCloudRevision);
                    if (!isOperationCurrent() || GameDataManager.Generation != generation) return false;
                    if (acknowledgement == null) throw new InvalidOperationException("Cloud upload has no acknowledgement.");
                    GameDataManager.AcknowledgeCloudUpload(attempt, acknowledgement.ServerRevision);
                    GameDataManager.SetConflictDeferred(null, null);
                }

                ClearConflict();
                return true;
            }
            catch (Exception exception)
            {
                LastResolutionError = "cloud_save_conflict_retry";
                Debug.LogWarning($"[CloudSave] Conflict choice retained ({exception.GetType().Name}).");
                return false;
            }
            finally { _isResolutionActive = false; }
        }

        public void ClearConflict()
        {
            if (CurrentConflict == null) return;
            var playerId = CurrentConflict.LocalSnapshot.PlayerId;
            if (!string.IsNullOrWhiteSpace(GameDataManager.GetJournalJson(JournalFeature, playerId)))
                GameDataManager.ExecuteTechnicalTransaction(() =>
                {
                    GameDataManager.SetJournalJson(JournalFeature, null, playerId);
                    if (GameDataManager.ActiveConflictOwner == playerId) GameDataManager.SetActiveConflictOwner(null);
                });
            CurrentConflict = null;
            NotifyConflictResolved();
        }

        private void NotifyConflictResolved()
        {
            var handlers = ConflictResolved;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogError($"[CloudSave] Conflict subscriber failed ({exception.GetType().Name})."); }
            }
        }

        private void NotifyConflictDetected()
        {
            var handlers = ConflictDetected;
            if (handlers == null) return;
            foreach (Action<CloudSaveConflict> handler in handlers.GetInvocationList())
            {
                try { handler(CurrentConflict); }
                catch (Exception exception) { Debug.LogError($"[CloudSave] Conflict subscriber failed ({exception.GetType().Name})."); }
            }
        }
    }
}
