namespace GameManagement.Progress
{
    /// <summary>
    /// Неизменяемый итог XP, уже начисленного за один победный забег.
    /// </summary>
    public sealed class RunExperienceResult
    {
        public int TotalExperience { get; }

        public int PlayerLevelBefore { get; }

        public int ExperiencePointsBefore { get; }

        public int PlayerLevelAfter { get; }

        public int ExperiencePointsAfter { get; }

        public bool LevelUp { get; }

        public RunExperienceResult(
            int totalExperience,
            int playerLevelBefore,
            int experiencePointsBefore,
            int playerLevelAfter,
            int experiencePointsAfter)
        {
            TotalExperience = totalExperience;
            PlayerLevelBefore = playerLevelBefore;
            ExperiencePointsBefore = experiencePointsBefore;
            PlayerLevelAfter = playerLevelAfter;
            ExperiencePointsAfter = experiencePointsAfter;
            LevelUp = playerLevelAfter > playerLevelBefore;
        }
    }
}
