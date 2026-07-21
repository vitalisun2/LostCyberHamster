using System;

namespace GameManagement.CloudSave
{
    /// <summary>Описывает две целые ветки одного аккаунта, ожидающие выбора игрока.</summary>
    public sealed class CloudSaveConflict
    {
        /// <summary>Создаёт независимые данные локальной и облачной веток.</summary>
        public CloudSaveConflict(
            CloudSaveSnapshot localSnapshot,
            CloudSaveReadResult cloudVersion)
        {
            if (localSnapshot == null)
                throw new ArgumentNullException(nameof(localSnapshot));
            if (cloudVersion == null)
                throw new ArgumentNullException(nameof(cloudVersion));

            LocalSnapshot = Clone(localSnapshot);
            CloudVersion = new CloudSaveReadResult(
                Clone(cloudVersion.Snapshot),
                cloudVersion.ServerRevision,
                cloudVersion.ServerModifiedAtUtc);
        }

        /// <summary>Полный локальный pending.</summary>
        public CloudSaveSnapshot LocalSnapshot { get; }

        /// <summary>Полная облачная версия с подтверждённой server revision.</summary>
        public CloudSaveReadResult CloudVersion { get; }

        /// <summary>Полный облачный снимок.</summary>
        public CloudSaveSnapshot CloudSnapshot => CloudVersion.Snapshot;

        private static CloudSaveSnapshot Clone(CloudSaveSnapshot snapshot)
        {
            return CloudSaveSnapshotCodec.Deserialize(CloudSaveSnapshotCodec.Serialize(snapshot));
        }
    }
}
