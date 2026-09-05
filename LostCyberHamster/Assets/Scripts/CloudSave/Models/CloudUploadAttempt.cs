using System;

namespace GameManagement.CloudSave.Models
{
    /// <summary>Позволяет подтвердить отправленную revision после потери сетевого ответа.</summary>
    [Serializable]
    public sealed class CloudUploadAttempt
    {
        public string ProfileId;
        public string OwnerPlayerId;
        public long LocalRevision;
        public string PayloadHash;
        public string ExpectedCloudRevision;
    }
}
