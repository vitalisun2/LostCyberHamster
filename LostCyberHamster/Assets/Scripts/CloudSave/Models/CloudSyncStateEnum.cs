namespace GameManagement.CloudSave.Models
{
    /// <summary>Ситуация синхронизации прогресса.</summary>
    public enum CloudSyncStateEnum
    {
        /// <summary>Облачное сохранение отсутствует.</summary>
        CloudMissing,

        /// <summary>Локальный и облачный прогресс совпадают.</summary>
        Synchronized,

        /// <summary>Изменился только локальный прогресс.</summary>
        LocalChanged,

        /// <summary>Изменился только облачный прогресс.</summary>
        CloudChanged,

        /// <summary>Локальный и облачный прогресс изменились.</summary>
        Conflict
    }
}
