using System;
using GameManagement.CloudSave.Models;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Управляет текущим снимком синхронизации.</summary>
    public sealed class SnapshotService
    {
        /// <summary>Ключ снимка.</summary>
        private const string SnapshotKey = "CloudSave_.PendingSnapshot";

        public SnapshotService()
        {
            // Восстанавливаем снимок.
            if (!PlayerPrefs.HasKey(SnapshotKey))
                return;

            Snapshot = CloudSaveSnapshot.FromJson(PlayerPrefs.GetString(SnapshotKey));
        }

        /// <summary>Текущий снимок.</summary>
        public CloudSaveSnapshot Snapshot { get; private set; }

        /// <summary>Ставит снимок в очередь.</summary>
        public void SetPending(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            Snapshot = snapshot;
            Save();
        }

        /// <summary>Удаляет текущий снимок.</summary>
        public void Clear()
        {
            Snapshot = null;
            PlayerPrefs.DeleteKey(SnapshotKey);
            PlayerPrefs.Save();
        }

        /// <summary>Удаляет pending, только если подтверждён тот же runtime-снимок.</summary>
        public bool ClearIfCurrent(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (!ReferenceEquals(Snapshot, snapshot))
                return false;

            Clear();
            return true;
        }

        /// <summary>Сохраняет текущий снимок.</summary>
        private void Save()
        {
            PlayerPrefs.SetString(SnapshotKey, Snapshot.ToJson());
            PlayerPrefs.Save();
        }
    }
}
