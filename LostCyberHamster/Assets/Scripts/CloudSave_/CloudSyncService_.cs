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

        /// <summary>Не допускает параллельные отправки.</summary>
        private bool _isUploadActive;

        /// <summary>Запрещает обработку новых событий.</summary>
        private bool _isDisposed;

        public CloudSyncService_(
            AccountService accountService,
            ICloudSaveVersionStore_ versionStore,
            ICloudSaveGateway_ gateway,
            SnapshotService_ snapshotService)
        {
            // Сохраняем зависимости.
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));

            // Подписываемся на события.
            _accountService.CurrentGuestLinked += OnAccountLinked;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
        }

        #region Обработка событий
        /// <summary>Создаёт первое сохранение после привязки аккаунта.</summary>
        private async void OnAccountLinked(string playerId)
        {
            if (_isDisposed)
                return;

            try
            {
                await CreateFirstCloudSaveAsync(playerId);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Создаёт сохранение после локального сохранения.</summary>
        private async void OnCheckpointCommitted(CheckpointReason reason)
        {
            if (_isDisposed)
                return;
            if (reason == CheckpointReason.AccountLinked)
                return;

            try
            {
                await CreateCloudSaveAsync();
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Основные методы

        /// <summary>Создаёт первое облачное сохранение.</summary>
        private async Task CreateFirstCloudSaveAsync(string linkedPlayerId)
        {
            // Проверяем, можно ли создать первое сохранение.
            if (!_accountService.TryGetLinkedPlayerId(out var playerId) ||
                !string.Equals(playerId, linkedPlayerId, StringComparison.Ordinal))
                return;
            if (_versionStore.HasConfirmedVersion(playerId))
                return;
            if (_snapshotService.Snapshot != null &&
                !string.Equals(_snapshotService.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                return;

            // Создаём первый снимок прогресса.
            if (_snapshotService.Snapshot == null)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AccountLinked);
                _snapshotService.SetPending(new CloudSaveSnapshot_(
                    playerId,
                    GameDataManager.PlayerData.ToJson()));
            }

            // Отправляем подготовленный снимок.
            await UploadPendingSnapshotAsync();
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

            // Отправляем подготовленный снимок.
            await UploadPendingSnapshotAsync();
        }

        /// <summary>Отправляет ожидающий снимок.</summary>
        private async Task UploadPendingSnapshotAsync()
        {
            if (_isUploadActive)
                return;
            _isUploadActive = true;

            try
            {
                // Отправляем все подготовленные снимки.
                while (_snapshotService.Snapshot != null)
                {
                    var snapshot = _snapshotService.Snapshot;

                    // Выбираем безопасный способ записи.
                    var cloud = await _gateway.LoadSnapshotAsync();
                    CloudSaveVersion_ version;
                    if (cloud == null)
                    {
                        version = await _gateway.SaveSnapshotAsync(snapshot, null);
                    }
                    else if (IsSameSnapshot(cloud.Snapshot, snapshot))
                    {
                        version = cloud.Version;
                    }
                    else if (CanUpdateSnapshot(cloud, snapshot))
                    {
                        version = await _gateway.SaveSnapshotAsync(
                            snapshot,
                            cloud.Version.ServerRevision);
                    }
                    else
                    {
                        return;
                    }

                    // Подтверждаем отправленный снимок.
                    _versionStore.SaveConfirmedVersion(snapshot.PlayerId, version.ServerRevision);
                    if (ReferenceEquals(_snapshotService.Snapshot, snapshot))
                        _snapshotService.Clear();
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
        /// <summary>Определяет, что снимок уже загружен.</summary>
        private static bool IsSameSnapshot(
            CloudSaveSnapshot_ cloudSnapshot,
            CloudSaveSnapshot_ pendingSnapshot)
        {
            return string.Equals(
                       cloudSnapshot.PlayerId,
                       pendingSnapshot.PlayerId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       cloudSnapshot.PlayerDataJson,
                       pendingSnapshot.PlayerDataJson,
                       StringComparison.Ordinal);
        }

        /// <summary>Разрешает безопасно обновить облачный снимок.</summary>
        private bool CanUpdateSnapshot(
            CloudSaveReadResult_ cloud,
            CloudSaveSnapshot_ pendingSnapshot)
        {
            if (!string.Equals(
                    cloud.Snapshot.PlayerId,
                    pendingSnapshot.PlayerId,
                    StringComparison.Ordinal))
                return false;

            var confirmedRevision = _versionStore.GetConfirmedRevision(pendingSnapshot.PlayerId);
            return string.Equals(
                confirmedRevision,
                cloud.Version.ServerRevision,
                StringComparison.Ordinal);
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
        }
    }
}
