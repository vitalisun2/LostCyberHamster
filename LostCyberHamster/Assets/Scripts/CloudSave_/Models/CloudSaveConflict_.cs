using System;
using GameManagement.CloudSave_.Gateway;

namespace GameManagement.CloudSave_.Models
{
    /// <summary>Хранит локальный и облачный прогресс конфликта.</summary>
    public sealed class CloudSaveConflict_
    {
        public CloudSaveConflict_(
            CloudSaveSnapshot_ localSnapshot,
            CloudSaveReadResult_ cloudSave)
        {
            LocalSnapshot = localSnapshot
                ?? throw new ArgumentNullException(nameof(localSnapshot));
            CloudSave = cloudSave
                ?? throw new ArgumentNullException(nameof(cloudSave));
        }

        /// <summary>Локальный прогресс.</summary>
        public CloudSaveSnapshot_ LocalSnapshot { get; }

        /// <summary>Облачный прогресс.</summary>
        public CloudSaveReadResult_ CloudSave { get; }
    }
}
