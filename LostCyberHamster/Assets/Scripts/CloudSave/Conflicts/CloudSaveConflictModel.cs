using System;

namespace GameManagement.CloudSave
{
    /// <summary>Описывает две целые ветки одного аккаунта, ожидающие выбора игрока.</summary>
    public sealed class CloudSaveConflictModel
    {
        public CloudSaveConflictModel(
            CloudSaveSnapshotDto localSnapshot,
            CloudSaveReadResult cloudVersion)
        {
            // Проверяем обе ветки до создания модели конфликта.
            if (localSnapshot == null)
                throw new ArgumentNullException(nameof(localSnapshot));
            if (cloudVersion == null)
                throw new ArgumentNullException(nameof(cloudVersion));

            // Изолируем данные конфликта от последующих изменений runtime-снимков.
            LocalSnapshot = Clone(localSnapshot);
            CloudVersion = new CloudSaveReadResult(
                Clone(cloudVersion.Snapshot),
                cloudVersion.ServerRevision,
                cloudVersion.ServerModifiedAtUtc);
        }

        /// <summary>Полный локальный pending.</summary>
        public CloudSaveSnapshotDto LocalSnapshot { get; }

        /// <summary>Полная облачная версия с подтверждённой server revision.</summary>
        public CloudSaveReadResult CloudVersion { get; }

        /// <summary>Полный облачный снимок.</summary>
        public CloudSaveSnapshotDto CloudSnapshot => CloudVersion.Snapshot;

        /// <summary>Создаёт независимую копию снимка через общий codec.</summary>
        private static CloudSaveSnapshotDto Clone(CloudSaveSnapshotDto snapshot)
        {
            return CloudSaveSnapshotCodec.Deserialize(CloudSaveSnapshotCodec.Serialize(snapshot));
        }
    }
}
