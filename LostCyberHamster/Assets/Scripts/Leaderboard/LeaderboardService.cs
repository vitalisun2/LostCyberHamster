using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts;
using GameManagement.Progress;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using LeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

namespace GameManagement.Leaderboard
{
    /// <summary>
    /// Сохраняет и получает недельные результаты игрока через Unity Leaderboards.
    /// </summary>
    public sealed class LeaderboardService
    {
        private const int _topCount = 50;

        /// <summary>
        /// Проверяет недельный рекорд и публикует успешный забег только при улучшении.
        /// </summary>
        public async Task<LeaderboardSubmissionResult> SubmitSuccessfulRunAsync(
            LevelProgressKey levelKey,
            int runScore)
        {
            // Проверяем результат и выбираем таблицу.
            if (runScore < 0)
                throw new ArgumentOutOfRangeException(nameof(runScore));

            var leaderboardId = ResolveLeaderboardId(
                levelKey.LocationId,
                levelKey.PartOfDayId);

            // Загружаем лучший score одного забега текущей weekly location+part таблицы.
            var weeklyBest = await GetPlayerWeeklyBestRunScoreAsync(
                leaderboardId);

            // Сравниваем один забег с прежним weekly best всей location+part таблицы.
            if (weeklyBest.HasEntry && runScore <= weeklyBest.Score)
            {
                return new LeaderboardSubmissionResult(
                    weeklyBest.Score,
                    weeklyBest.Score,
                    false);
            }

            // Публикуем новый weekly best одного успешного забега без накопления score.
            await LeaderboardsService.Instance.AddPlayerScoreAsync(
                leaderboardId,
                runScore);

            return new LeaderboardSubmissionResult(
                weeklyBest.Score,
                runScore,
                true);
        }

        /// <summary>
        /// Возвращает лучший weekly score текущего игрока для location и части дня уровня.
        /// </summary>
        public async Task<int> GetPlayerWeeklyBestRunScoreAsync(
            LevelProgressKey levelKey)
        {
            // Выбираем реальную weekly таблицу по scope уровня.
            var leaderboardId = ResolveLeaderboardId(
                levelKey.LocationId,
                levelKey.PartOfDayId);

            // Для первой попытки возвращаем нулевую базу нового рекорда.
            var weeklyBest = await GetPlayerWeeklyBestRunScoreAsync(
                leaderboardId);
            return weeklyBest.Score;
        }

        /// <summary>
        /// Получает топ-50 и отдельную позицию текущего игрока.
        /// </summary>
        public async Task<(
            IReadOnlyList<LeaderboardEntry> Top,
            LeaderboardEntry CurrentPlayer)> GetResultsAsync(
            string locationId,
            string partOfDayId)
        {
            // Получаем верхние позиции выбранной таблицы.
            var leaderboardId = ResolveLeaderboardId(locationId, partOfDayId);
            var topScores = await LeaderboardsService.Instance.GetScoresAsync(
                leaderboardId,
                new GetScoresOptions
                {
                    Offset = 0,
                    Limit = _topCount
                });

            // Получаем игрока отдельно, потому что он может быть вне топа.
            LeaderboardEntry currentPlayer = null;
            try
            {
                currentPlayer = await LeaderboardsService.Instance.GetPlayerScoreAsync(
                    leaderboardId);
            }
            catch (LeaderboardsException exception) when (
                exception.Reason == LeaderboardsExceptionReason.EntryNotFound ||
                exception.Reason == LeaderboardsExceptionReason.ScoreSubmissionRequired)
            {
                // Игрок ещё не публиковал результат в этой таблице.
            }

            return (topScores.Results, currentPlayer);
        }

        private static async Task<(bool HasEntry, int Score)>
            GetPlayerWeeklyBestRunScoreAsync(string leaderboardId)
        {
            try
            {
                var playerScore =
                    await LeaderboardsService.Instance.GetPlayerScoreAsync(
                        leaderboardId);
                return (true, checked((int)playerScore.Score));
            }
            catch (LeaderboardsException exception) when (
                exception.Reason == LeaderboardsExceptionReason.EntryNotFound ||
                exception.Reason ==
                LeaderboardsExceptionReason.ScoreSubmissionRequired)
            {
                return (false, 0);
            }
        }

        /// <summary>
        /// Возвращает серверный идентификатор таблицы локации и части дня.
        /// </summary>
        private static string ResolveLeaderboardId(
            string locationId,
            string partOfDayId)
        {
            // Проверяем и нормализуем составные части ключа.
            if (string.IsNullOrWhiteSpace(locationId))
                throw new ArgumentException(
                    "Location identifier must be provided.",
                    nameof(locationId));

            if (string.IsNullOrWhiteSpace(partOfDayId))
                throw new ArgumentException(
                    "Part-of-day identifier must be provided.",
                    nameof(partOfDayId));

            var normalizedLocationId = locationId.Trim().ToLowerInvariant();
            var normalizedPartOfDayId = partOfDayId.Trim().ToLowerInvariant();

            // Выбираем только заранее настроенную таблицу.
            return (normalizedLocationId, normalizedPartOfDayId) switch
            {
                ("01_new_york", "morning") => Consts.NewYorkMorningLeaderboardId,
                ("01_new_york", "afternoon") => Consts.NewYorkAfternoonLeaderboardId,
                ("01_new_york", "evening") => Consts.NewYorkEveningLeaderboardId,
                ("01_new_york", "night") => Consts.NewYorkNightLeaderboardId,
                ("02_paris", "morning") => Consts.ParisMorningLeaderboardId,
                ("02_paris", "afternoon") => Consts.ParisAfternoonLeaderboardId,
                ("02_paris", "evening") => Consts.ParisEveningLeaderboardId,
                ("02_paris", "night") => Consts.ParisNightLeaderboardId,
                ("03_barcelona", "morning") => Consts.BarcelonaMorningLeaderboardId,
                ("03_barcelona", "afternoon") => Consts.BarcelonaAfternoonLeaderboardId,
                ("03_barcelona", "evening") => Consts.BarcelonaEveningLeaderboardId,
                ("03_barcelona", "night") => Consts.BarcelonaNightLeaderboardId,
                _ => throw new ArgumentException(
                    $"Leaderboard is not configured for {locationId}:{partOfDayId}.")
            };
        }
    }
}
