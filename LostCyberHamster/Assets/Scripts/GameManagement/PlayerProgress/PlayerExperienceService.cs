using System;
using Vues.GameCore.Quests;

namespace GameManagement.Progress
{
    /// <summary>
    /// Начисляет подтверждённый XP и повышает Player Level по фиксированному порогу.
    /// </summary>
    public sealed class PlayerExperienceService
    {
        /// <summary>
        /// Количество XP, необходимое для повышения Player Level.
        /// </summary>
        public const int PlayerLevelThreshold = 240;

        private const int ExperiencePerImprovedStar = 10;
        private const int WeeklyLeaderboardRecordExperienceReward = 50;
        private const int DailyQuestExperienceReward = 20;
        private const int StorylineQuestExperienceReward = 60;

        /// <summary>
        /// Начисляет XP только за положительный прирост лучшего результата по звёздам.
        /// </summary>
        public bool GrantExperienceForImprovedStars(
            PlayerData playerData,
            LevelProgressKey progressKey,
            LevelProgressSnapshot updatedSnapshot)
        {
            // Проверяем контекст до чтения сохранённого и обновлённого progress.
            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            if (updatedSnapshot == null)
            {
                throw new ArgumentNullException(nameof(updatedSnapshot));
            }

            // Извлекаем оба best stars и начисляем XP только за улучшение.
            var previousBestStars =
                playerData.Progress.GetStars(progressKey);
            var updatedBestStars =
                updatedSnapshot.GetStars(progressKey);
            var improvedStars = Math.Max(
                0,
                updatedBestStars - previousBestStars);
            if (improvedStars == 0)
            {
                return false;
            }

            return GrantExperience(
                playerData,
                checked(improvedStars * ExperiencePerImprovedStar));
        }

        /// <summary>
        /// Начисляет XP за подтверждённый сервером новый weekly leaderboard record.
        /// </summary>
        public bool GrantExperienceForWeeklyLeaderboardRecord(
            PlayerData playerData)
        {
            return GrantExperience(
                playerData,
                WeeklyLeaderboardRecordExperienceReward);
        }

        /// <summary>
        /// Начисляет XP за одноразово полученную награду дневного квеста.
        /// </summary>
        public bool GrantExperienceForClaimedDailyQuest(PlayerData playerData)
        {
            return GrantExperience(
                playerData,
                DailyQuestExperienceReward);
        }

        /// <summary>
        /// Начисляет XP за одноразово полученную награду сюжетного квеста.
        /// </summary>
        public bool GrantExperienceForClaimedStorylineQuest(
            PlayerData playerData)
        {
            return GrantExperience(
                playerData,
                StorylineQuestExperienceReward);
        }

        /// <summary>
        /// Начисляет XP, переносит остаток и возвращает признак хотя бы одного повышения Player Level.
        /// </summary>
        private bool GrantExperience(
            PlayerData playerData,
            int experienceReward)
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
            var previousExperiencePoints = playerData.ExperiencePoints;
            var previousPlayerLevel = playerData.PlayerLevel;
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

            if (playerLevelsGained > 0)
            {
                GameEventsManager.PlayerStateChanged(
                    PlayerStateIds.PlayerLevel,
                    PlayerStateEntityIds.Player);
            }

            // Фиксируем каждое фактическое начисление в economy diagnostics.
            DebugManager.DiagEconomy(
                $"[PlayerExperience] grant amount={experienceReward} " +
                $"xp={previousExperiencePoints}->{playerData.ExperiencePoints} " +
                $"level={previousPlayerLevel}->{playerData.PlayerLevel}");
            return playerLevelsGained > 0;
        }
    }
}
