namespace GameManagement.Progress
{
    /// <summary>Неизменяемая квитанция начисления XP; публикация разрешена после сохранения.</summary>
    public readonly struct ExperienceGrantResult
    {
        public string Source { get; }
        public int Amount { get; }
        public int FromLevel { get; }
        public int ToLevel { get; }
        public int DevelopmentPointsEarned { get; }
        public int CoinsEarned { get; }
        public bool LevelChanged => ToLevel > FromLevel;

        public ExperienceGrantResult(string source, int amount, int fromLevel, int toLevel,
            int developmentPointsEarned, int coinsEarned)
        {
            Source = source;
            Amount = amount;
            FromLevel = fromLevel;
            ToLevel = toLevel;
            DevelopmentPointsEarned = developmentPointsEarned;
            CoinsEarned = coinsEarned;
        }
    }
}
