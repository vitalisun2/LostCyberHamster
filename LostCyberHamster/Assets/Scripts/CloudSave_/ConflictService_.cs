using System;
using System.Threading.Tasks;
using GameManagement.CloudSave_.Gateway;
using GameManagement.CloudSave_.Models;
using GameManagement.CloudSave_.Version;

namespace GameManagement.CloudSave_
{
    /// <summary>Управляет выбором между локальным и облачным прогрессом.</summary>
    public sealed class ConflictService_
    {
        /// <summary>Читает и записывает облачный снимок.</summary>
        private readonly ICloudSaveGateway_ _gateway;

        /// <summary>Хранит подтверждённые облачные версии.</summary>
        private readonly ICloudSaveVersionStore_ _versionStore;

        /// <summary>Управляет локальным снимком.</summary>
        private readonly SnapshotService_ _snapshotService;

        public ConflictService_(
            ICloudSaveGateway_ gateway,
            ICloudSaveVersionStore_ versionStore,
            SnapshotService_ snapshotService)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        }

        /// <summary>Текущий конфликт.</summary>
        public CloudSaveConflict_ CurrentConflict { get; private set; }

        /// <summary>Возникает при обнаружении конфликта.</summary>
        public event Action<CloudSaveConflict_> ConflictDetected;

        /// <summary>Сохраняет обнаруженный конфликт.</summary>
        public void SetConflict(
            CloudSaveSnapshot_ localSnapshot,
            CloudSaveReadResult_ cloudSave)
        {
            // Сохраняем обе версии конфликта.
            CurrentConflict = new CloudSaveConflict_(localSnapshot, cloudSave);

            // Сообщаем об обнаруженном конфликте.
            ConflictDetected?.Invoke(CurrentConflict);
        }

        /// <summary>Выбирает облачный прогресс.</summary>
        public Task ResolveWithCloudAsync()
        {
            var conflict = CurrentConflict;

            // Применяем и сохраняем облачный прогресс.
            var playerData = PlayerData.FromJson(conflict.CloudSave.Snapshot.PlayerDataJson);
            GameDataManager.ReplacePlayerData(playerData);

            // Подтверждаем выбор и завершаем конфликт.
            _versionStore.SaveConfirmedVersion(
                conflict.CloudSave.Snapshot.PlayerId,
                conflict.CloudSave.Version.ServerRevision);
            _snapshotService.Clear();
            CurrentConflict = null;

            return Task.CompletedTask;
        }

        /// <summary>Выбирает локальный прогресс.</summary>
        public async Task ResolveWithLocalAsync()
        {
            var conflict = CurrentConflict;

            // Записываем локальный прогресс поверх облачной версии.
            var version = await _gateway.SaveSnapshotAsync(
                conflict.LocalSnapshot,
                conflict.CloudSave.Version.ServerRevision);

            // Подтверждаем выбор и завершаем конфликт.
            _versionStore.SaveConfirmedVersion(
                conflict.LocalSnapshot.PlayerId,
                version.ServerRevision);
            _snapshotService.Clear();
            CurrentConflict = null;
        }
    }
}
