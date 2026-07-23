using System;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;

namespace GameManagement.CloudSave.Gateway
{
    /// <summary>Хранит загруженный снимок и его версию.</summary>
    public sealed class CloudSaveReadResult
    {
        public CloudSaveReadResult(CloudSaveSnapshot snapshot, CloudSaveVersion version)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        /// <summary>Загруженный снимок.</summary>
        public CloudSaveSnapshot Snapshot { get; }

        /// <summary>Подтверждённая облачная версия.</summary>
        public CloudSaveVersion Version { get; }
    }
}
