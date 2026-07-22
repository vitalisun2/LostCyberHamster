using System;
using GameManagement.CloudSave_.Models;
using GameManagement.CloudSave_.Version;

namespace GameManagement.CloudSave_.Gateway
{
    /// <summary>Хранит загруженный снимок и его версию.</summary>
    public sealed class CloudSaveReadResult_
    {
        public CloudSaveReadResult_(CloudSaveSnapshot_ snapshot, CloudSaveVersion_ version)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        /// <summary>Загруженный снимок.</summary>
        public CloudSaveSnapshot_ Snapshot { get; }

        /// <summary>Подтверждённая облачная версия.</summary>
        public CloudSaveVersion_ Version { get; }
    }
}
