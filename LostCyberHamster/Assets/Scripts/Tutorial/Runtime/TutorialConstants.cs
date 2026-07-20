using System;
using Assets.Scripts.System;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Содержит адреса сцен, уровней и чистые predicates tutorial-маршрута.
    /// </summary>
    public static class TutorialConstants
    {
        public const string CoreLessonLevelAddress = "01_New_York/Morning/Tutorial Level";
        public const string SuperHitLessonLevelAddress = "01_New_York/Morning/Tutorial Level 2";
        public const string FirstGameplayLevelAddress = "01_New_York/Morning/level_01";
        public const string FirstGameplayLevelKey = "level_01";
        public const string MenuSceneName = "Menu";
        public const string GameSceneName = "Game";

        public static bool IsTutorialLevel(string levelAddress)
        {
            return IsCoreLessonLevel(levelAddress) || IsSuperHitLessonLevel(levelAddress);
        }

        public static bool IsCoreLessonLevel(string levelAddress)
        {
            return AddressesMatch(levelAddress, CoreLessonLevelAddress);
        }

        public static bool IsSuperHitLessonLevel(string levelAddress)
        {
            return AddressesMatch(levelAddress, SuperHitLessonLevelAddress);
        }

        /// <summary>
        /// Проверяет прямой адрес, короткий ключ и canonical address из level catalog.
        /// </summary>
        public static bool IsFirstGameplayLevel(string levelAddress)
        {
            if (string.IsNullOrWhiteSpace(levelAddress))
            {
                return false;
            }

            if (AddressesMatch(levelAddress, FirstGameplayLevelAddress)
                || AddressesMatch(levelAddress, FirstGameplayLevelKey))
            {
                return true;
            }

            if (!LevelCatalogService.TryFindLevel(NormalizeAddress(levelAddress), out var descriptor))
            {
                return false;
            }

            return AddressesMatch(descriptor.Address, FirstGameplayLevelAddress);
        }

        private static bool AddressesMatch(string left, string right)
        {
            return string.Equals(
                NormalizeAddress(left),
                NormalizeAddress(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAddress(string address)
        {
            return address?.Replace('\\', '/').Trim();
        }
    }
}
