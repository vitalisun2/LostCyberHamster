using System;
using System.Threading.Tasks;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Управляет выбором между локальным и облачным прогрессом.</summary>
    public sealed class ConflictService
    {
        /// <summary>Читает и записывает облачный снимок.</summary>
        private readonly ICloudSaveGateway _gateway;

        /// <summary>Хранит подтверждённые облачные версии.</summary>
        private readonly ICloudSaveVersionStore _versionStore;

        /// <summary>Управляет локальным снимком.</summary>
        private readonly SnapshotService _snapshotService;

        /// <summary>Не допускает параллельный выбор двух веток.</summary>
        private bool _isResolutionActive;

        public ConflictService(
            ICloudSaveGateway gateway,
            ICloudSaveVersionStore versionStore,
            SnapshotService snapshotService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        }

        /// <summary>Текущий конфликт.</summary>
        public CloudSaveConflict CurrentConflict { get; private set; }

        /// <summary>Возникает при обнаружении конфликта.</summary>
        public event Action<CloudSaveConflict> ConflictDetected;

        /// <summary>Возникает после разрешения конфликта.</summary>
        public event Action ConflictResolved;

        /// <summary>Сохраняет обнаруженный конфликт.</summary>
        public void SetConflict(
            CloudSaveSnapshot localSnapshot,
            CloudSaveReadResult cloudSave)
        {
            if (localSnapshot == null)
                throw new ArgumentNullException(nameof(localSnapshot));
            if (cloudSave == null)
                throw new ArgumentNullException(nameof(cloudSave));
            if (!string.Equals(
                    localSnapshot.PlayerId,
                    cloudSave.Snapshot.PlayerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Conflict snapshot owner mismatch.");
            }

            // Сохраняем обе версии и сообщаем UI об актуальном выборе.
            CurrentConflict = new CloudSaveConflict(localSnapshot, cloudSave);
            NotifyConflictDetected();
        }

        /// <summary>Обновляет локальную ветку новым checkpoint.</summary>
        public bool TryUpdateLocalSnapshot(CloudSaveSnapshot localSnapshot)
        {
            if (localSnapshot == null)
                throw new ArgumentNullException(nameof(localSnapshot));
            if (CurrentConflict == null)
                return false;
            if (!string.Equals(
                    localSnapshot.PlayerId,
                    CurrentConflict.CloudSave.Snapshot.PlayerId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            SetConflict(localSnapshot, CurrentConflict.CloudSave);
            return true;
        }

        /// <summary>Перечитывает и применяет подтверждённую пользователем облачную ветку.</summary>
        public async Task<bool> ResolveWithCloudAsync(
            string playerId,
            Func<bool> isOperationCurrent)
        {
            var conflict = CurrentConflict;
            if (!CanStartResolution(playerId, isOperationCurrent, conflict))
                return false;

            _isResolutionActive = true;
            try
            {
                // Повторно читаем облако перед применением показанной ветки.
                var latestCloud = await _gateway.LoadSnapshotAsync();
                if (!isOperationCurrent())
                    return false;
                if (!IsCloudOwnedBy(latestCloud, playerId))
                {
                    Debug.LogError("[CloudSave] Cloud conflict choice failed: current cloud unavailable.");
                    return false;
                }

                // Изменившееся облако требует показать игроку актуальный выбор.
                if (!string.Equals(
                        latestCloud.Version.ServerRevision,
                        conflict.CloudSave.Version.ServerRevision,
                        StringComparison.Ordinal))
                {
                    SetConflict(GetCurrentLocalSnapshot(playerId, conflict), latestCloud);
                    return false;
                }

                // Применяем выбранные данные только внутри текущего lifecycle владельца.
                var playerData = PlayerData.FromJson(latestCloud.Snapshot.PlayerDataJson);
                if (!isOperationCurrent())
                    return false;

                GameDataManager.ReplacePlayerData(playerData);
                if (!isOperationCurrent())
                    return false;

                _versionStore.SaveConfirmedVersion(
                    playerId,
                    latestCloud.Version.ServerRevision);
                _snapshotService.ClearIfCurrent(conflict.LocalSnapshot);
                ClearConflict();
                return true;
            }
            catch (Exception exception)
            {
                if (isOperationCurrent())
                {
                    Debug.LogError(
                        $"[CloudSave] Cloud conflict choice failed ({exception.GetType().Name}).");
                }

                return false;
            }
            finally
            {
                _isResolutionActive = false;
            }
        }

        /// <summary>Записывает выбранную локальную ветку поверх актуального write lock.</summary>
        public async Task<bool> ResolveWithLocalAsync(
            string playerId,
            Func<bool> isOperationCurrent)
        {
            var conflict = CurrentConflict;
            if (!CanStartResolution(playerId, isOperationCurrent, conflict))
                return false;

            _isResolutionActive = true;
            try
            {
                // Перечитываем актуальный write lock непосредственно перед записью.
                var latestCloud = await _gateway.LoadSnapshotAsync();
                if (!isOperationCurrent())
                    return false;
                if (!IsCloudOwnedBy(latestCloud, playerId))
                {
                    Debug.LogError("[CloudSave] Local conflict choice failed: current cloud unavailable.");
                    return false;
                }

                var version = await _gateway.SaveSnapshotAsync(
                    conflict.LocalSnapshot,
                    latestCloud.Version.ServerRevision);
                if (!isOperationCurrent())
                    return false;
                if (version == null)
                    throw new InvalidOperationException("Cloud Save returned no write result.");

                // Подтверждаем exact отправленный pending; новый checkpoint остаётся в очереди.
                _versionStore.SaveConfirmedVersion(playerId, version.ServerRevision);
                _snapshotService.ClearIfCurrent(conflict.LocalSnapshot);
                ClearConflict();
                return true;
            }
            catch (Exception exception)
            {
                if (isOperationCurrent())
                {
                    Debug.LogError(
                        $"[CloudSave] Local conflict choice failed ({exception.GetType().Name}).");
                }

                return false;
            }
            finally
            {
                _isResolutionActive = false;
            }
        }

        /// <summary>Проверяет владельца и lifecycle нового выбора.</summary>
        private bool CanStartResolution(
            string playerId,
            Func<bool> isOperationCurrent,
            CloudSaveConflict conflict)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            if (isOperationCurrent == null)
                throw new ArgumentNullException(nameof(isOperationCurrent));

            return !_isResolutionActive &&
                   conflict != null &&
                   isOperationCurrent() &&
                   string.Equals(
                       conflict.LocalSnapshot.PlayerId,
                       playerId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       conflict.CloudSave.Snapshot.PlayerId,
                       playerId,
                       StringComparison.Ordinal);
        }

        /// <summary>Возвращает последний owned pending для обновлённого выбора.</summary>
        private CloudSaveSnapshot GetCurrentLocalSnapshot(
            string playerId,
            CloudSaveConflict fallbackConflict)
        {
            var pending = _snapshotService.Snapshot;
            return pending != null &&
                   string.Equals(pending.PlayerId, playerId, StringComparison.Ordinal)
                ? pending
                : fallbackConflict.LocalSnapshot;
        }

        /// <summary>Проверяет наличие и владельца повторно прочитанного облака.</summary>
        private static bool IsCloudOwnedBy(
            CloudSaveReadResult cloudSave,
            string playerId)
        {
            return cloudSave != null &&
                   string.Equals(
                       cloudSave.Snapshot.PlayerId,
                       playerId,
                       StringComparison.Ordinal);
        }

        /// <summary>Очищает конфликт и сообщает потребителям о продолжении.</summary>
        public void ClearConflict()
        {
            if (CurrentConflict == null)
                return;

            CurrentConflict = null;
            var handlers = ConflictResolved;
            if (handlers == null)
                return;

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[CloudSave] Conflict resolution subscriber failed ({exception.GetType().Name}).");
                }
            }
        }

        /// <summary>Безопасно сообщает подписчикам об актуальном конфликте.</summary>
        private void NotifyConflictDetected()
        {
            var handlers = ConflictDetected;
            if (handlers == null)
                return;

            foreach (Action<CloudSaveConflict> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(CurrentConflict);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[CloudSave] Conflict subscriber failed ({exception.GetType().Name}).");
                }
            }
        }
    }
}
