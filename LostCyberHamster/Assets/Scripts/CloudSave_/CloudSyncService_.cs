using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameManagement.CloudSave_.Gateway;
using GameManagement.CloudSave_.Models;
using GameManagement.CloudSave_.Version;

namespace GameManagement.CloudSave_
{
    /// <summary>Создаёт и отправляет облачные сохранения.</summary>
    public sealed class CloudSyncService_ : IDisposable
    {
        /// <summary>Предоставляет текущий аккаунт.</summary>
        private readonly AccountService _accountService;

        /// <summary>Хранит подтверждённые облачные версии.</summary>
        private readonly ICloudSaveVersionStore_ _versionStore;

        /// <summary>Читает и записывает облачный снимок.</summary>
        private readonly ICloudSaveGateway_ _gateway;

        /// <summary>Управляет локальным снимком.</summary>
        private readonly SnapshotService_ _snapshotService;

        /// <summary>Управляет конфликтом прогресса.</summary>
        private readonly ConflictService_ _conflictService;

        /// <summary>Не допускает параллельные отправки.</summary>
        private bool _isUploadActive;

        /// <summary>Запрещает обработку новых событий.</summary>
        private bool _isDisposed;

        public CloudSyncService_(
            AccountService accountService,
            ICloudSaveVersionStore_ versionStore,
            ICloudSaveGateway_ gateway,
            SnapshotService_ snapshotService,
            ConflictService_ conflictService)
        {
            // Сохраняем зависимости.
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));

            // Подписываемся на события.
            _accountService.CurrentGuestLinked += OnAccountLinked;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed += OnApplicationResumed;
        }

        #region Обработка событий
        /// <summary>Синхронизирует прогресс после привязки аккаунта.</summary>
        private async void OnAccountLinked(string playerId)
        {
            if (_isDisposed)
                return;

            try
            {
                await SynchronizeProgressAsync();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Синхронизирует прогресс после локального сохранения.</summary>
        private async void OnCheckpointCommitted(CheckpointReason reason)
        {
            if (_isDisposed)
                return;
            if (reason == CheckpointReason.AccountLinked)
                return;

            try
            {
                if (reason == CheckpointReason.MenuEntered)
                {
                    await SynchronizeProgressAsync();
                    return;
                }

                await CreateCloudSaveAsync();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Синхронизирует прогресс после возврата в игру.</summary>
        private async void OnApplicationResumed()
        {
            if (_isDisposed)
                return;

            try
            {
                await SynchronizeProgressAsync();
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Основные методы

        /// <summary>Создаёт первое облачное сохранение.</summary>
        private async Task CreateFirstCloudSaveAsync(string playerId)
        {
            // Готовим первый снимок, если его ещё нет.
            if (_snapshotService.Snapshot == null)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AccountLinked);
                _snapshotService.SetPending(new CloudSaveSnapshot_(
                    playerId,
                    GameDataManager.PlayerData.ToJson()));
            }

            // Отправляем подготовленный снимок.
            await UploadPendingSnapshotAsync(null);
        }

        /// <summary>Создаёт облачное сохранение.</summary>
        private async Task CreateCloudSaveAsync()
        {
            // Проверяем, можно ли создать сохранение.
            if (!_accountService.TryGetLinkedPlayerId(out var playerId))
                return;

            // Готовим последний прогресс к отправке.
            _snapshotService.SetPending(new CloudSaveSnapshot_(
                playerId,
                GameDataManager.PlayerData.ToJson()));

            // Синхронизируем новое сохранение.
            await SynchronizeProgressAsync();
        }

        /// <summary>Восстанавливает прогресс существующего аккаунта.</summary>
        public async Task RestoreProgressAsync(string playerId)
        {
            // Загружаем прогресс аккаунта.
            var cloudSave = await _gateway.LoadSnapshotAsync();
            if (cloudSave == null)
            {
                await CreateFirstCloudSaveAsync(playerId);
                return;
            }

            // Заменяем локальный прогресс облачным.
            ApplyCloudProgress(playerId, cloudSave);
            _snapshotService.Clear();
        }

        /// <summary>Синхронизирует локальный и облачный прогресс.</summary>
        private async Task SynchronizeProgressAsync()
        {
            if (!_accountService.TryGetLinkedPlayerId(out var playerId) ||
                _isUploadActive)
                return;

            // Получаем текущее состояние синхронизации.
            var cloudSave = await _gateway.LoadSnapshotAsync();
            var confirmedRevision = _versionStore.GetConfirmedRevision(playerId);
            var syncState = GetSyncState(
                cloudSave,
                _snapshotService.Snapshot,
                confirmedRevision);

            // Выполняем нужный сценарий.
            switch (syncState)
            {
                case CloudSyncStateEnum_.CloudMissing:
                    await CreateFirstCloudSaveAsync(playerId);
                    break;

                case CloudSyncStateEnum_.LocalChanged:
                    await UploadPendingSnapshotAsync(
                        cloudSave.Version.ServerRevision);
                    break;

                case CloudSyncStateEnum_.CloudChanged:
                    ApplyCloudProgress(playerId, cloudSave);
                    break;

                case CloudSyncStateEnum_.Conflict:
                    _conflictService.SetConflict(
                        _snapshotService.Snapshot,
                        cloudSave);
                    break;

                case CloudSyncStateEnum_.Synchronized:
                    break;
            }
        }

        /// <summary>Отправляет ожидающий снимок.</summary>
        private async Task UploadPendingSnapshotAsync(string expectedRevision)
        {
            if (_isUploadActive || _snapshotService.Snapshot == null)
                return;

            _isUploadActive = true;

            try
            {
                // Отправляем все подготовленные снимки.
                while (_snapshotService.Snapshot != null)
                {
                    var snapshot = _snapshotService.Snapshot;
                    var version = await _gateway.SaveSnapshotAsync(
                        snapshot,
                        expectedRevision);

                    // Подтверждаем отправленный снимок.
                    _versionStore.SaveConfirmedVersion(snapshot.PlayerId, version.ServerRevision);
                    if (ReferenceEquals(_snapshotService.Snapshot, snapshot))
                        _snapshotService.Clear();

                    expectedRevision = version.ServerRevision;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _isUploadActive = false;
            }
        }

        #endregion

        #region Вспомогательные методы
        /// <summary>Определяет текущую ситуацию синхронизации.</summary>
        private static CloudSyncStateEnum_ GetSyncState(
            CloudSaveReadResult_ cloudSave,
            CloudSaveSnapshot_ pendingSnapshot,
            string confirmedRevision)
        {
            // Определяем изменения с обеих сторон.
            var hasCloudSave = cloudSave != null;
            var hasPending = pendingSnapshot != null;
            var cloudChanged = hasCloudSave &&
                !string.Equals(
                    confirmedRevision,
                    cloudSave.Version.ServerRevision,
                    StringComparison.Ordinal);

            // Выбираем подходящий сценарий.
            return (hasCloudSave, hasPending, cloudChanged) switch
            {
                (false, _, _) => CloudSyncStateEnum_.CloudMissing,
                (true, false, false) => CloudSyncStateEnum_.Synchronized,
                (true, true, false) => CloudSyncStateEnum_.LocalChanged,
                (true, false, true) => CloudSyncStateEnum_.CloudChanged,
                (true, true, true) => CloudSyncStateEnum_.Conflict
            };
        }

        /// <summary>Применяет облачный прогресс.</summary>
        private void ApplyCloudProgress(
            string playerId,
            CloudSaveReadResult_ cloudSave)
        {
            // Заменяем локальный прогресс.
            var restoredData = PlayerData.FromJson(cloudSave.Snapshot.PlayerDataJson);
            GameDataManager.ReplacePlayerData(restoredData);

            // Запоминаем принятую облачную версию.
            _versionStore.SaveConfirmedVersion(
                playerId,
                cloudSave.Version.ServerRevision);
        }

        #endregion

        /// <summary>Останавливает сервис и убирает подписки.</summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _accountService.CurrentGuestLinked -= OnAccountLinked;
            PlayerProgressCommitter.CommitCompleted -= OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed -= OnApplicationResumed;
        }
    }
}
