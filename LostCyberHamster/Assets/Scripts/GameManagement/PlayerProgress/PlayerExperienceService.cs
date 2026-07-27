using System;

namespace GameManagement.Progress
{
    /// <summary>
    /// Начисляет подтверждённый XP и повышает Player Level по фиксированному порогу.
    /// </summary>
    public sealed class PlayerExperienceService
    {
        private const int PlayerLevelThreshold = 240;

        /// <summary>
        /// Начисляет XP, переносит остаток и возвращает признак хотя бы одного повышения Player Level.
        /// </summary>
        public bool GrantExperience(PlayerData playerData, int experienceReward)
        {
            // Проверяем награду и нормализованное состояние игрока.
            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            if (experienceReward <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(experienceReward),
                    "Experience reward must be positive.");
            }

            if (playerData.ExperiencePoints < 0 || playerData.PlayerLevel < 1)
            {
                throw new InvalidOperationException(
                    "Player experience data must be normalized before granting experience.");
            }

            // Рассчитываем новое состояние без частичного изменения PlayerData.
            var updatedExperiencePoints = checked(
                playerData.ExperiencePoints + experienceReward);
            var playerLevelsGained =
                updatedExperiencePoints / PlayerLevelThreshold;
            var updatedPlayerLevel = checked(
                playerData.PlayerLevel + playerLevelsGained);

            // Применяем все переходы по 240 XP и сохраняем остаток.
            playerData.ExperiencePoints =
                updatedExperiencePoints % PlayerLevelThreshold;
            playerData.PlayerLevel = updatedPlayerLevel;
            return playerLevelsGained > 0;
        }
    }
}
