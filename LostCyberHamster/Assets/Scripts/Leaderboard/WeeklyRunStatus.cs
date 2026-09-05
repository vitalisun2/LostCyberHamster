namespace GameManagement.Leaderboard
{
    /// <summary>Состояние сохранённой отправки и подтверждения награды.</summary>
    public enum WeeklyRunStatus
    {
        Pending,
        ConfirmedImprovement,
        NotImproved,
        Expired,
        Unconfirmed,
        AwaitingLocalSave,
        LocalOnly
    }
}
