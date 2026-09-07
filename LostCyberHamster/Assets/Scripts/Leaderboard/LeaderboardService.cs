using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using LeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

namespace GameManagement.Leaderboard
{
    /// <summary>
    /// Сохраняет и получает недельные результаты игрока через Unity Leaderboards.
    /// </summary>
    public sealed class LeaderboardService
    {
        private const int _topCount = 50;

        public static IReadOnlyList<string> ConfiguredLeaderboardIds { get; } = new[]
        {
            Consts.NewYorkMorningLeaderboardId, Consts.NewYorkAfternoonLeaderboardId,
            Consts.NewYorkEveningLeaderboardId, Consts.NewYorkNightLeaderboardId,
            Consts.ParisMorningLeaderboardId, Consts.ParisAfternoonLeaderboardId,
            Consts.ParisEveningLeaderboardId, Consts.ParisNightLeaderboardId,
            Consts.BarcelonaMorningLeaderboardId, Consts.BarcelonaAfternoonLeaderboardId,
            Consts.BarcelonaEveningLeaderboardId, Consts.BarcelonaNightLeaderboardId
        };

        /// <summary>Получает текущую серверную версию таблицы.</summary>
        public Task<LeaderboardVersions> GetSeasonAsync(string leaderboardId) =>
            LeaderboardsService.Instance.GetVersionsAsync(leaderboardId);

        /// <summary>Читает собственный результат вместе с подтверждающими метаданными.</summary>
        public async Task<LeaderboardEntry> GetPlayerEntryAsync(string leaderboardId)
        {
            try
            {
                return await LeaderboardsService.Instance.GetPlayerScoreAsync(
                    leaderboardId, new GetPlayerScoreOptions { IncludeMetadata = true });
            }
            catch (LeaderboardsException exception) when (
                exception.Reason == LeaderboardsExceptionReason.EntryNotFound ||
                exception.Reason == LeaderboardsExceptionReason.ScoreSubmissionRequired)
            {
                return null;
            }
        }

        /// <summary>Отправляет результат с серверным запретом записи в другую неделю.</summary>
        public Task<LeaderboardEntry> SubmitVersionedScoreAsync(
            string leaderboardId, int score, string versionId, WeeklyScoreMetadata metadata) =>
            LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score,
                new AddPlayerScoreOptions { VersionId = versionId, Metadata = metadata });

        /// <summary>Получает верхние 50 позиций одной таблицы единственным сетевым запросом.</summary>
        public async Task<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(string leaderboardId)
        {
            var scores = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId,
                new GetScoresOptions { Offset = 0, Limit = _topCount });
            return scores.Results;
        }

        /// <summary>
        /// Возвращает серверный идентификатор таблицы локации и части дня.
        /// </summary>
        public static string ResolveLeaderboardId(
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
