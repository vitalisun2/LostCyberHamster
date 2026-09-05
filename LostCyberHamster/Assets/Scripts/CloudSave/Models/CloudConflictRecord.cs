using System;

namespace GameManagement.CloudSave.Models
{
    /// <summary>Сохраняет обе ветки отложенного выбора вместе с локальным профилем.</summary>
    [Serializable]
    public sealed class CloudConflictRecord
    {
        public string ProfileId;
        public string PlayerId;
        public string LocalSnapshotJson;
        public string CloudSnapshotJson;
        public string CloudRevision;
    }
}
