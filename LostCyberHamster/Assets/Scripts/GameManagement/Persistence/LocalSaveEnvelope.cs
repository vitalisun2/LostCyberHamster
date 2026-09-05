using System;
using System.Collections.Generic;
using GameManagement.CloudSave.Models;

namespace GameManagement
{
    /// <summary>Хранит согласованный локальный прогресс и журналы устройства одним снимком.</summary>
    [Serializable]
    public sealed class LocalSaveEnvelope
    {
        public string Format = "LostCyberHamster.LocalSave";
        public int Schema = 1;
        public string ProfileId;
        public string OwnerPlayerId;
        public bool LegacyOwnerUnassigned;
        public long LocalRevision = 1;
        public long LastSyncedRevision;
        public string BaseCloudRevision;
        public string LastCloudSyncUtc;
        public PlayerData PlayerData;
        public List<LocalFeatureJournal> Journals = new();
        public CloudUploadAttempt UploadAttempt;
        public string DeferredConflictOwner;
        public string DeferredConflictRevision;
        public string ActiveConflictOwner;
    }
}
