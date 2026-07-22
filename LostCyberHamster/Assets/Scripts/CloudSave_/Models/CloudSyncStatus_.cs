namespace GameManagement.CloudSave_.Models
{
    /// <summary>Определяет, можно ли продолжать облачную синхронизацию.</summary>
    internal enum CloudSyncStatus_
    {
        /// <summary>Отправлять нечего.</summary>
        None,

        /// <summary>Снимок ещё не подтверждён облаком.</summary>
        Pending,

        /// <summary>Синхронизацию нельзя продолжить без разбора различий.</summary>
        NeedsReview
    }
}
