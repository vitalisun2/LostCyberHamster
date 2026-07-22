using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит конфликт и выполняет выбор одной целой cloud/local ветки.</summary>
    public sealed class CloudSaveConflictService
    {
        /// <summary>Загружает и сохраняет облачные снимки.</summary>
        private readonly ICloudSaveGateway _gateway;

        /// <summary>Предоставляет владельца текущего связанного аккаунта.</summary>
        private readonly AccountService _accountService;

        /// <summary>Сохраняет локальную ветку до подтверждения выбора.</summary>
        private readonly SnapshotUploadService _uploadService;

        /// <summary>Не допускает параллельного выбора двух веток.</summary>
        private bool _isConflictResolutionActive;

        public CloudSaveConflictService(
            ICloudSaveGateway gateway,
            AccountService accountService,
            SnapshotUploadService uploadService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        }

        /// <summary>Текущие две независимо изменённые ветки, ожидающие выбора.</summary>
        public CloudSaveConflictModel CurrentConflict { get; private set; }

        /// <summary>Возникает при обнаружении или обновлении данных конфликта.</summary>
        public event Action<CloudSaveConflictModel> ConflictDetected;

        /// <summary>Показывает, что выбор ветки уже выполняется.</summary>
        internal bool IsResolutionActive => _isConflictResolutionActive;

        /// <summary>Проверяет актуальность выбранного облачного снимка и целиком применяет его локально.</summary>
        internal async Task<CloudSaveConflictResolutionOutcome<CloudSaveReadResult>> TryResolveWithCloudAsync(
            CancellationToken operationToken)
        {
            // Проверяем активный конфликт и владельца до сетевого вызова.
            var conflict = CurrentConflict;
            if (_isConflictResolutionActive ||
                conflict == null ||
                !_accountService.TryGetLinkedPlayerId(out var playerId) ||
                !string.Equals(conflict.LocalSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);
            }

            // Блокируем параллельный выбор и загружаем актуальную cloud-ветку.
            _isConflictResolutionActive = true;
            try
            {
                var latestCloud = await _gateway.LoadSnapshotAsync();
                if (!IsOperationCurrent(playerId, operationToken))
                    return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);

                // Отклоняем отсутствующую или чужую cloud-ветку.
                if (latestCloud == null ||
                    !string.Equals(latestCloud.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    Debug.LogError("[CloudSave] Cloud conflict choice failed: current cloud unavailable.");
                    return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);
                }

                // Обновляем конфликт, если облако изменилось после показа выбора.
                if (!ReferenceEquals(CurrentConflict, conflict) ||
                    !string.Equals(
                        latestCloud.ServerRevision,
                        conflict.CloudVersion.ServerRevision,
                        StringComparison.Ordinal))
                {
                    SetConflict(CurrentConflict?.LocalSnapshot ?? conflict.LocalSnapshot, latestCloud);
                    return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);
                }

                // Восстанавливаем и проверяем выбранные данные до commit.
                if (!CloudSaveSnapshotRestorer.TryRestore(
                        latestCloud.Snapshot,
                        out var restoredData,
                        out var rejectionReason))
                {
                    Debug.LogWarning($"[CloudSave] Conflict cloud snapshot rejected ({rejectionReason}).");
                    return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);
                }

                // Целиком применяем проверенную cloud-ветку.
                try
                {
                    GameDataManager.ReplacePlayerData(restoredData);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[CloudSave] Conflict cloud apply failed ({exception.GetType().Name}).");
                    return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);
                }

                Debug.Log("[CloudSave] Cloud conflict choice applied.");
                return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Success(
                    conflict,
                    latestCloud);
            }
            catch (Exception exception)
            {
                // Устаревшая операция завершается без сообщения об ошибке.
                if (!IsOperationCurrent(playerId, operationToken))
                    return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);

                Debug.LogError($"[CloudSave] Cloud conflict choice failed ({exception.GetType().Name}).");
                return CloudSaveConflictResolutionOutcome<CloudSaveReadResult>.Failure(conflict);
            }
            finally
            {
                // Всегда разрешаем следующий выбор после завершения attempt.
                _isConflictResolutionActive = false;
            }
        }

        /// <summary>Записывает выбранный локальный снимок поверх актуальной облачной версии.</summary>
        internal async Task<CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>> TryResolveWithLocalAsync(
            CancellationToken operationToken)
        {
            // Проверяем активный конфликт и владельца до сетевого вызова.
            var conflict = CurrentConflict;
            if (_isConflictResolutionActive ||
                conflict == null ||
                !_accountService.TryGetLinkedPlayerId(out var playerId) ||
                !string.Equals(conflict.LocalSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);
            }

            // Блокируем параллельный выбор и загружаем актуальную cloud revision.
            _isConflictResolutionActive = true;
            CloudSaveReadResult latestCloud = null;
            try
            {
                latestCloud = await _gateway.LoadSnapshotAsync();
                if (!IsOperationCurrent(playerId, operationToken))
                    return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);

                // Отклоняем отсутствующую или чужую cloud-ветку.
                if (latestCloud == null ||
                    !string.Equals(latestCloud.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    Debug.LogError("[CloudSave] Local conflict choice failed: current cloud unavailable.");
                    return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);
                }

                if (!ReferenceEquals(CurrentConflict, conflict))
                    return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);

                // Перебазируем local-ветку и сохраняем её до отправки.
                conflict.LocalSnapshot.BaseRevision = latestCloud.ServerRevision;
                _uploadService.PersistSnapshot(conflict.LocalSnapshot);

                // Целиком записываем local-ветку поверх актуальной revision.
                var result = await _gateway.SaveSnapshotAsync(conflict.LocalSnapshot)
                    ?? throw new InvalidOperationException("Cloud Save returned no write result.");
                if (!IsOperationCurrent(playerId, operationToken))
                    return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);

                Debug.Log("[CloudSave] Local conflict choice uploaded.");
                return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Success(
                    conflict,
                    result);
            }
            catch (Exception exception)
            {
                // Устаревшая операция завершается без изменения текущего конфликта.
                if (!IsOperationCurrent(playerId, operationToken))
                    return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);

                // Сохраняем последнюю прочитанную cloud-ветку для повторного выбора.
                if (latestCloud != null)
                {
                    SetConflict(
                        CurrentConflict?.LocalSnapshot ?? conflict.LocalSnapshot,
                        latestCloud);
                }

                Debug.LogError($"[CloudSave] Local conflict choice failed ({exception.GetType().Name}).");
                return CloudSaveConflictResolutionOutcome<CloudSaveWriteResult>.Failure(conflict);
            }
            finally
            {
                // Всегда разрешаем следующий выбор после завершения attempt.
                _isConflictResolutionActive = false;
            }
        }

        /// <summary>Заменяет текущий конфликт и безопасно уведомляет подписчиков.</summary>
        private void SetConflict(
            CloudSaveSnapshotDto localSnapshot,
            CloudSaveReadResult cloudVersion)
        {
            // Создаём независимую модель веток для runtime и UI.
            CurrentConflict = new CloudSaveConflictModel(localSnapshot, cloudVersion);
            var handlers = ConflictDetected;
            if (handlers == null)
                return;

            // Ошибка одного подписчика не мешает остальным получить конфликт.
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

        /// <summary>Обновляет локальную ветку текущего конфликта новым checkpoint.</summary>
        internal bool TryUpdateLocalSnapshot(CloudSaveSnapshotDto localSnapshot)
        {
            if (CurrentConflict == null)
                return false;

            SetConflict(localSnapshot, CurrentConflict.CloudVersion);
            return true;
        }

        /// <summary>Определяет divergence и публикует новый конфликт.</summary>
        internal bool TryDetectConflict(
            CloudSaveSnapshotDto pendingSnapshot,
            CloudSaveSnapshotDto latestLocalSnapshot,
            CloudSaveReadResult cloudVersion)
        {
            if (AreSnapshotsEquivalent(pendingSnapshot, cloudVersion.Snapshot) ||
                string.Equals(
                    pendingSnapshot.BaseRevision,
                    cloudVersion.ServerRevision,
                    StringComparison.Ordinal))
            {
                return false;
            }

            SetConflict(latestLocalSnapshot, cloudVersion);
            return true;
        }

        /// <summary>Сравнивает полный локальный снимок с облачным.</summary>
        internal static bool AreSnapshotsEquivalent(
            CloudSaveSnapshotDto first,
            CloudSaveSnapshotDto second)
        {
            return string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal) &&
                   string.Equals(first.Revision, second.Revision, StringComparison.Ordinal) &&
                   string.Equals(first.BaseRevision, second.BaseRevision, StringComparison.Ordinal) &&
                   string.Equals(first.SavedAtUtc, second.SavedAtUtc, StringComparison.Ordinal) &&
                   string.Equals(first.PlayerDataJson, second.PlayerDataJson, StringComparison.Ordinal);
        }

        /// <summary>Завершает выбранный конфликт после успешной общей синхронизации.</summary>
        internal void CompleteResolution(CloudSaveConflictModel resolvedConflict)
        {
            if (ReferenceEquals(CurrentConflict, resolvedConflict))
                ClearConflict();
        }

        /// <summary>Очищает разрешённый или потерявший актуальность конфликт.</summary>
        internal void ClearConflict()
        {
            CurrentConflict = null;
        }

        /// <summary>Проверяет lifecycle операции и владельца связанного аккаунта.</summary>
        private bool IsOperationCurrent(
            string playerId,
            CancellationToken operationToken)
        {
            return !operationToken.IsCancellationRequested &&
                   _accountService.TryGetLinkedPlayerId(out var currentPlayerId) &&
                   string.Equals(currentPlayerId, playerId, StringComparison.Ordinal);
        }
    }
}
