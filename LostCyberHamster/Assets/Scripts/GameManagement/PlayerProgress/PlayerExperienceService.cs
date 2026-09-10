using System;
using Assets.Scripts.Tutorial;
using Vues.GameCore;
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
        public const int TutorialExperienceReward = 150;

        public const int ExperiencePerFirstWin = 25;
        public const int ExperiencePerImprovedStar = 2;
        public const int WeeklyLeaderboardRecordExperienceReward = 5;

        /// <summary>Начисляет стартовые XP один раз; вызывающий сохраняет результат до публикации.</summary>
        public bool GrantExperienceForTutorialCompletion(PlayerData playerData)
        {
            if (playerData == null)
                throw new ArgumentNullException(nameof(playerData));
            if (playerData.HasReceivedTutorialExperience)
                return false;

            bool levelChanged = GrantExperience(playerData, TutorialExperienceReward, notify: false);
            playerData.HasReceivedTutorialExperience = true;
            return levelChanged;
        }

        /// <summary>
        /// Начисляет XP за первую победу и новые best-звёзды до замены сохранённого progress.
        /// </summary>
        public ExperienceGrantResult GrantExperienceForLevelCompletion(
            PlayerData playerData,
            LevelProgressKey progressKey,
            LevelProgressSnapshot updatedSnapshot,
            bool notify = true)
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

            // Best=0 означает отсутствие победы; стабильный ключ различает уровни кампании.
            var previousBestStars =
                playerData.Progress.GetStars(progressKey);
            var updatedBestStars =
                updatedSnapshot.GetStars(progressKey);
            var improvedStars = Math.Max(
                0,
                updatedBestStars - previousBestStars);
            int amount = checked(improvedStars * ExperiencePerImprovedStar +
                (previousBestStars == 0 && updatedBestStars > 0 ? ExperiencePerFirstWin : 0));
            int fromLevel = playerData.PlayerLevel;
            int previousPoints = playerData.DevelopmentPoints;
            int previousMoney = playerData.Money;
            if (amount > 0) GrantExperience(playerData, amount, notify);
            return new ExperienceGrantResult(progressKey.ToString(), amount, fromLevel, playerData.PlayerLevel,
                playerData.DevelopmentPoints - previousPoints, playerData.Money - previousMoney);
        }

        /// <summary>
        /// Применяет сохранённое решение weekly: 0/5 XP либо восстановление прежней квитанции 50 XP.
        /// </summary>
        public bool GrantExperienceForWeeklyLeaderboardRecord(
            PlayerData playerData, int awardedExperience = WeeklyLeaderboardRecordExperienceReward, bool notify = true)
        {
            if (awardedExperience != 0 && awardedExperience != WeeklyLeaderboardRecordExperienceReward && awardedExperience != 50)
                throw new ArgumentOutOfRangeException(nameof(awardedExperience));
            return awardedExperience > 0 && GrantExperience(playerData, awardedExperience, notify);
        }

        /// <summary>
        /// Начисляет XP за одноразово полученную награду дневного квеста.
        /// </summary>
        public bool GrantExperienceForClaimedDailyQuest(PlayerData playerData, bool notify = true)
        {
            return GrantExperience(
                playerData,
                QuestExperienceRewardPolicy.DailyReward, notify);
        }

        /// <summary>
        /// Начисляет XP конкретного полученного квеста; нулевая награда сохраняет Claim.
        /// </summary>
        public bool GrantExperienceForClaimedQuest(
            PlayerData playerData, Quest quest, bool notify = true)
        {
            int amount = QuestExperienceRewardPolicy.GetReward(quest);
            return amount > 0 && GrantExperience(playerData, amount, notify);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Начисляет точное количество XP через production-математику для DEV testing tools.
        /// </summary>
        public bool GrantExperienceForTesting(
            PlayerData playerData,
            int experienceReward, bool notify = true)
        {
            return GrantExperience(playerData, experienceReward, notify);
        }
#endif

        /// <summary>Публикует повышение уровня после успешного сохранения транзакции.</summary>
        public static void PublishCommittedLevelChange(bool playerLevelChanged, string source = "reward")
        {
            if (playerLevelChanged)
            {
                ResourceManager.NotifyBalancesChangedAfterCommit();
                FirstSessionTelemetry.Record("development_point_committed", source,
                    GameDataManager.PlayerData?.PlayerLevel ?? 1);
                GameEventsManager.PlayerStateChanged(
                    PlayerStateIds.PlayerLevel, PlayerStateEntityIds.Player);
            }
        }

        /// <summary>
        /// Начисляет XP, переносит остаток и возвращает признак хотя бы одного повышения Player Level.
        /// </summary>
        private bool GrantExperience(
            PlayerData playerData,
            int experienceReward, bool notify)
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
            CharacterDevelopmentService.GrantForLevelUps(
                playerData,
                playerLevelsGained);

            if (notify)
                PublishCommittedLevelChange(playerLevelsGained > 0);

            // Фиксируем каждое фактическое начисление в economy diagnostics.
            if (notify) DebugManager.DiagEconomy(
                $"[PlayerExperience] grant amount={experienceReward} " +
                $"xp={previousExperiencePoints}->{playerData.ExperiencePoints} " +
                $"level={previousPlayerLevel}->{playerData.PlayerLevel}");
            return playerLevelsGained > 0;
        }
    }
}
