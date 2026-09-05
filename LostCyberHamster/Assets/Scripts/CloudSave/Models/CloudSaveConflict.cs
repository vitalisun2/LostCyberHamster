using System;
using GameManagement.CloudSave.Gateway;

namespace GameManagement.CloudSave.Models
{
    /// <summary>Хранит локальный и облачный прогресс конфликта.</summary>
    public sealed class CloudSaveConflict
    {
        public CloudSaveConflict(
            CloudSaveSnapshot localSnapshot,
            CloudSaveReadResult cloudSave)
        {
            LocalSnapshot = localSnapshot
                ?? throw new ArgumentNullException(nameof(localSnapshot));
            CloudSave = cloudSave;
            LocalRevision = GameDataManager.LocalRevision;
            ProfileGeneration = GameDataManager.Generation;
        }

        /// <summary>Локальный прогресс.</summary>
        public CloudSaveSnapshot LocalSnapshot { get; }

        /// <summary>Облачный прогресс.</summary>
        public CloudSaveReadResult CloudSave { get; }
        public long LocalRevision { get; }
        public long ProfileGeneration { get; }
        public string CloudRevision => CloudSave?.Version.ServerRevision ?? "missing";
    }
}
