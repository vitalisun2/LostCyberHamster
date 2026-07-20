using System;
using Assets.Scripts.System;

namespace Assets.Scripts.TutorialOld
{
    /// <summary>
    /// Общие адреса и настройки первого tutorial-сценария.
    /// </summary>
    public static class TutorialConstants
    {
        public const string TutorialLevelAddress = "01_New_York/Morning/Tutorial Level";
        public const string TutorialSuperHitLevelAddress = "01_New_York/Morning/Tutorial Level 2";
        public const string FirstGameplayLevelAddress = "01_New_York/Morning/level_01";
        public const string FirstGameplayLevelKey = "level_01";
        public const string MenuSceneName = "Menu";
        public const string GameSceneName = "Game";

        /// <summary>
        /// Проверяет, что текущий адрес указывает на отдельный tutorial level.
        /// </summary>
        public static bool IsTutorialLevel(string levelAddress)
        {
            var normalized = levelAddress?.Trim();
            return string.Equals(normalized, TutorialLevelAddress, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, TutorialSuperHitLevelAddress, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSuperHitTutorialLevel(string levelAddress)
        {
            return string.Equals(
                levelAddress?.Trim(),
                TutorialSuperHitLevelAddress,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, что адрес или короткий ключ указывают на первый уровень первой локации.
        /// </summary>
        public static bool IsFirstGameplayLevel(string levelAddress)
        {
            if (string.IsNullOrWhiteSpace(levelAddress))
            {
                return false;
            }

            var normalized = levelAddress.Replace('\\', '/').Trim();
            if (string.Equals(normalized, FirstGameplayLevelAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, FirstGameplayLevelKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!LevelCatalogService.TryFindLevel(normalized, out var descriptor))
            {
                return false;
            }

            return string.Equals(
                descriptor.Address?.Trim(),
                FirstGameplayLevelAddress,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
