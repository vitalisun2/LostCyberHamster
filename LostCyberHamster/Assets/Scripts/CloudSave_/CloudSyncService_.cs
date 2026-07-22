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

        /// <summary>Снимок без подтверждения сервера.</summary>
        private CloudSaveSnapshot_ _pendingSnapshot;

        /// <summary>Состояние синхронизации.</summary>
        private CloudSyncStatus_ _status;

        /// <summary>Не допускает параллельные отправки.</summary>
        private bool _isUploadActive;

        /// <summary>Запрещает обработку новых событий.</summary>
        private bool _isDisposed;

        public CloudSyncService_(
            AccountService accountService,
            ICloudSaveVersionStore_ versionStore,
            ICloudSaveGateway_ gateway)
        {
            // Сохраняем зависимости.
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

            // Подписываемся на события.
            _accountService.CurrentGuestLinked += OnAccountLinked;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
        }

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
                _status = CloudSyncStatus_.Pending;
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
                _status = CloudSyncStatus_.Pending;
            }
        }

        #region First Cloud Save

        /// <summary>Создаёт первое облачное сохранение.</summary>
        private async Task CreateFirstCloudSaveAsync(string linkedPlayerId)
        {
            // Проверяем, можно ли создать первое сохранение.
            if (!_accountService.TryGetLinkedPlayerId(out var playerId) ||
                !string.Equals(playerId, linkedPlayerId, StringComparison.Ordinal))
                return;
            if (_versionStore.HasConfirmedVersion(playerId))
                return;
            if (_status == CloudSyncStatus_.NeedsReview)
                return;
            if (_pendingSnapshot != null &&
                !string.Equals(_pendingSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                _status = CloudSyncStatus_.NeedsReview;
                return;
            }

            // Создаём первый снимок прогресса.
            if (_pendingSnapshot == null)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AccountLinked);
                _pendingSnapshot = new CloudSaveSnapshot_(
                    playerId,
                    GameDataManager.PlayerData.ToJson());
                _status = CloudSyncStatus_.Pending;
            }

            // Отправляем подготовленный снимок.
            await UploadPendingSnapshotAsync();
        }

        #endregion

        #region Cloud Save

        /// <summary>Создаёт облачное сохранение.</summary>
        private async Task CreateCloudSaveAsync()
        {
            // Проверяем, можно ли создать сохранение.
            if (!_accountService.TryGetLinkedPlayerId(out var playerId))
                return;
            if (_status == CloudSyncStatus_.NeedsReview)
                return;

            // Готовим последний прогресс к отправке.
            _pendingSnapshot = new CloudSaveSnapshot_(
                playerId,
                GameDataManager.PlayerData.ToJson());
            _status = CloudSyncStatus_.Pending;

            // Отправляем подготовленный снимок.
            await UploadPendingSnapshotAsync();
        }

        #endregion

        #region Upload

        /// <summary>Отправляет ожидающий снимок.</summary>
        private async Task UploadPendingSnapshotAsync()
        {
            if (_isUploadActive)
                return;
            _isUploadActive = true;

            try
            {
                // Отправляем все подготовленные снимки.
                while (_pendingSnapshot != null &&
                       _status != CloudSyncStatus_.NeedsReview)
                {
                    var snapshot = _pendingSnapshot;

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
                        _status = CloudSyncStatus_.NeedsReview;
                        return;
                    }

                    // Подтверждаем отправленный снимок.
                    _versionStore.SaveConfirmedVersion(snapshot.PlayerId, version.ServerRevision);
                    if (ReferenceEquals(_pendingSnapshot, snapshot))
                    {
                        _pendingSnapshot = null;
                        _status = CloudSyncStatus_.None;
                    }
                }
            }
            catch (Exception)
            {
                _status = CloudSyncStatus_.Pending;
            }
            finally
            {
                _isUploadActive = false;
            }
        }

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
