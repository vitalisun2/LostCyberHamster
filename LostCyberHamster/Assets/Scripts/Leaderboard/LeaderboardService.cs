using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts;
using GameManagement.Progress;
using Unity.Services.Authentication;
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
        private static readonly SemaphoreSlim _submissionGate = new(1, 1);

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

        /// <summary>
        /// Последовательно проверяет weekly record и публикует забег только при улучшении.
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
            var playerId = GetCurrentPlayerId();

            // Сериализуем read/compare/write между всеми gameplay lifecycle и экземплярами сервиса.
            await _submissionGate.WaitAsync();
            try
            {
                EnsureCurrentPlayer(playerId);
                return await SubmitSuccessfulRunCoreAsync(
                    leaderboardId,
                    runScore,
                    playerId);
            }
            finally
            {
                _submissionGate.Release();
            }
        }

        /// <summary>
        /// Проверяет и сохраняет один забег внутри общей последовательной submission-секции.
        /// </summary>
        private static async Task<LeaderboardSubmissionResult>
            SubmitSuccessfulRunCoreAsync(
                string leaderboardId,
                int runScore,
                string playerId)
        {
            // Фиксируем серверную неделю до read/compare/write совместимого DEV-пути.
            var season = await LeaderboardsService.Instance.GetVersionsAsync(leaderboardId);
            EnsureCurrentPlayer(playerId);

            // Загружаем лучший score одного забега текущей weekly location+part таблицы.
            var weeklyBest = await GetPlayerWeeklyBestRunScoreAsync(
                leaderboardId);
            EnsureCurrentPlayer(playerId);

            // Сравниваем один забег с прежним weekly best всей location+part таблицы.
            if (weeklyBest.HasEntry && runScore <= weeklyBest.Score)
            {
                return new LeaderboardSubmissionResult(
                    playerId,
                    weeklyBest.Score,
                    weeklyBest.Score,
                    false);
            }

            // Публикуем новый weekly best одного успешного забега без накопления score.
            var accepted = await LeaderboardsService.Instance.AddPlayerScoreAsync(
                leaderboardId,
                runScore, new AddPlayerScoreOptions { VersionId = season.VersionId });
            EnsureCurrentPlayer(playerId);

            return new LeaderboardSubmissionResult(
                playerId,
                weeklyBest.Score,
                checked((int)accepted.Score),
                accepted.Score == runScore);
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
        /// Возвращает идентификатор текущей аутентифицированной identity.
        /// </summary>
        private static string GetCurrentPlayerId()
        {
            if (!AuthenticationService.Instance.IsSignedIn ||
                string.IsNullOrWhiteSpace(
                    AuthenticationService.Instance.PlayerId))
            {
                throw new InvalidOperationException(
                    "Leaderboard submission requires an authenticated player.");
            }

            return AuthenticationService.Instance.PlayerId;
        }

        /// <summary>
        /// Прерывает устаревший submission после смены account lifecycle.
        /// </summary>
        private static void EnsureCurrentPlayer(string playerId)
        {
            if (AuthenticationService.Instance.IsSignedIn &&
                string.Equals(
                    AuthenticationService.Instance.PlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new OperationCanceledException(
                "Leaderboard submission player changed during the operation.");
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
