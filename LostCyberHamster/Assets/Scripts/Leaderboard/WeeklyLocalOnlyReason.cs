namespace GameManagement.Leaderboard
{
    /// <summary>Причина локального результата; None сохраняет общий текст для старых записей.</summary>
    public enum WeeklyLocalOnlyReason
    {
        None,
        OwnerUnassigned,
        SeasonUnknown
    }
}
