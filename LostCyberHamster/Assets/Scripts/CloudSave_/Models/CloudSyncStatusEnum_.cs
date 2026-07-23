namespace GameManagement.CloudSave_.Models
{
    /// <summary>Состояние облачной синхронизации.</summary>
    public enum CloudSyncStatusEnum_
    {
        /// <summary>Прогресс сохранён в облаке.</summary>
        Saved,

        /// <summary>Прогресс отправляется в облако.</summary>
        Synchronizing,

        /// <summary>Прогресс ожидает отправки.</summary>
        Pending,

        /// <summary>Прогресс требует выбора версии.</summary>
        Conflict
    }
}
