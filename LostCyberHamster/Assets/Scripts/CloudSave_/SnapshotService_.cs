using System;
using GameManagement.CloudSave_.Models;
using UnityEngine;

namespace GameManagement.CloudSave_
{
    /// <summary>Управляет текущим снимком синхронизации.</summary>
    public sealed class SnapshotService_
    {
        /// <summary>Ключ снимка.</summary>
        private const string SnapshotKey = "CloudSave_.PendingSnapshot";

        public SnapshotService_()
        {
            // Восстанавливаем снимок.
            if (!PlayerPrefs.HasKey(SnapshotKey))
            {
                Status = CloudSyncStatusEnum_.None;
                return;
            }

            Snapshot = CloudSaveSnapshot_.FromJson(PlayerPrefs.GetString(SnapshotKey));

            Status = CloudSyncStatusEnum_.Pending;
        }

        /// <summary>Текущий снимок.</summary>
        public CloudSaveSnapshot_ Snapshot { get; private set; }

        /// <summary>Статус текущего снимка.</summary>
        public CloudSyncStatusEnum_ Status { get; private set; }

        /// <summary>Ставит снимок в очередь.</summary>
        public void SetPending(CloudSaveSnapshot_ snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            Snapshot = snapshot;
            Status = CloudSyncStatusEnum_.Pending;
            Save();
        }

        /// <summary>Удаляет текущий снимок.</summary>
        public void Clear()
        {
            Snapshot = null;
            Status = CloudSyncStatusEnum_.None;
            PlayerPrefs.DeleteKey(SnapshotKey);
            PlayerPrefs.Save();
        }

        /// <summary>Сохраняет текущий снимок.</summary>
        private void Save()
        {
            PlayerPrefs.SetString(SnapshotKey, Snapshot.ToJson());
            PlayerPrefs.Save();
        }
    }
}
