using System;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Передаёт одноразовый запрос навигации между игровой сценой и меню.
    /// </summary>
    public static class MenuNavigationRequest
    {
        private static string _leaderboardLocationId;
        private static string _leaderboardPartId;

        /// <summary>
        /// Сохраняет цель рейтинга до загрузки сцены меню.
        /// </summary>
        public static void OpenLeaderboard(string locationId, string partId)
        {
            // Проверяем обе части цели до изменения текущего запроса.
            if (string.IsNullOrWhiteSpace(locationId))
                throw new ArgumentException(
                    "Location identifier must be provided.",
                    nameof(locationId));

            if (string.IsNullOrWhiteSpace(partId))
                throw new ArgumentException(
                    "Part-of-day identifier must be provided.",
                    nameof(partId));

            // Перезаписываем только один ожидающий запрос.
            _leaderboardLocationId = locationId.Trim();
            _leaderboardPartId = partId.Trim();
        }

        /// <summary>
        /// Возвращает и сразу очищает ожидающую цель рейтинга.
        /// </summary>
        public static bool TryConsumeLeaderboard(
            out string locationId,
            out string partId)
        {
            // Сначала копируем значения для вызывающего кода.
            locationId = _leaderboardLocationId;
            partId = _leaderboardPartId;

            // Очищаем запрос независимо от его валидности.
            _leaderboardLocationId = null;
            _leaderboardPartId = null;

            return !string.IsNullOrWhiteSpace(locationId) &&
                   !string.IsNullOrWhiteSpace(partId);
        }
    }
}
