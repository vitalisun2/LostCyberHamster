using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Хранит единый timing второго input для super roof-to-roof action и planning travel.
    /// </summary>
    internal static class SuperJumpFromRoofOnRoofTiming
    {
        /// <summary>
        /// Safety-отступ от края double-jump upgrade window.
        /// </summary>
        private const float UpgradeWindowSafetySeconds = 0.05f;

        /// <summary>
        /// Задержка второго input для upgrade до super roof jump.
        /// </summary>
        public const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold - UpgradeWindowSafetySeconds;

        /// <summary>
        /// Дистанция мира, пройденная за время delay второго input.
        /// </summary>
        public const float UpgradeDelayTravel = UpgradeDelaySeconds * Consts.GameSpeedBase;
    }
}
