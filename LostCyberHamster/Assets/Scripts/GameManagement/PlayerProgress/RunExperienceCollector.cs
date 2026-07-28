using System;

namespace GameManagement.Progress
{
    /// <summary>
    /// Фиксирует XP до забега и собирает итог после всех начислений забега.
    /// </summary>
    public sealed class RunExperienceCollector
    {
        private readonly int _playerLevelBefore;
        private readonly int _experiencePointsBefore;

        public RunExperienceCollector(PlayerData playerData)
        {
            ValidatePlayerData(playerData);

            _playerLevelBefore = playerData.PlayerLevel;
            _experiencePointsBefore = playerData.ExperiencePoints;
        }

        /// <summary>
        /// Собирает неизменяемый итог из сохранённого начального и текущего состояний.
        /// </summary>
        public RunExperienceResult Collect(PlayerData playerData)
        {
            ValidatePlayerData(playerData);

            // Переводим переходы Level в общий XP и добавляем изменение остатка.
            int playerLevelsGained =
                playerData.PlayerLevel - _playerLevelBefore;
            int totalExperience = checked(
                playerLevelsGained *
                PlayerExperienceService.PlayerLevelThreshold +
                playerData.ExperiencePoints -
                _experiencePointsBefore);
            if (totalExperience < 0)
            {
                throw new InvalidOperationException(
                    "Run experience total cannot be negative.");
            }

            return new RunExperienceResult(
                totalExperience,
                _playerLevelBefore,
                _experiencePointsBefore,
                playerData.PlayerLevel,
                playerData.ExperiencePoints);
        }

        private static void ValidatePlayerData(PlayerData playerData)
        {
            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            if (playerData.PlayerLevel < 1 ||
                playerData.ExperiencePoints < 0 ||
                playerData.ExperiencePoints >=
                PlayerExperienceService.PlayerLevelThreshold)
            {
                throw new InvalidOperationException(
                    "Player experience data must be normalized.");
            }
        }
    }
}
