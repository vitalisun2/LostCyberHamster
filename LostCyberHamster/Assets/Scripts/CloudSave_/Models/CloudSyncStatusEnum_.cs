namespace GameManagement.CloudSave_.Models
{
    /// <summary>Определяет, можно ли продолжать облачную синхронизацию.</summary>
    public enum CloudSyncStatusEnum_
    {
        /// <summary>Отправлять нечего.</summary>
        None,

        /// <summary>Снимок ещё не подтверждён облаком.</summary>
        Pending
    }
}
