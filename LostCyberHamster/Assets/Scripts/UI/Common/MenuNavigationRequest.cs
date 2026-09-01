using System;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Передаёт одноразовый запрос навигации между игровой сценой и меню.
    /// </summary>
    public static class MenuNavigationRequest
    {
        private static ScreenEnum? _targetScreen;
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
            _targetScreen = ScreenEnum.LeaderboardScreen;
            _leaderboardLocationId = locationId.Trim();
            _leaderboardPartId = partId.Trim();
        }

        /// <summary>
        /// Сохраняет переход к развитию персонажа до загрузки Menu.
        /// </summary>
        public static void OpenCharacterDevelopment()
        {
            _targetScreen = ScreenEnum.CharacterDevelopmentScreen;
            _leaderboardLocationId = null;
            _leaderboardPartId = null;
        }

        /// <summary>
        /// Возвращает и сразу очищает ожидающий переход в Menu.
        /// </summary>
        public static bool TryConsume(
            out ScreenEnum targetScreen,
            out string locationId,
            out string partId)
        {
            // Сначала копируем единый запрос для вызывающего кода.
            bool hasRequest = _targetScreen.HasValue;
            targetScreen =
                _targetScreen ?? ScreenEnum.HomeScreen;
            locationId = _leaderboardLocationId;
            partId = _leaderboardPartId;

            // Очищаем запрос независимо от его валидности.
            _targetScreen = null;
            _leaderboardLocationId = null;
            _leaderboardPartId = null;

            return hasRequest;
        }
    }
}
