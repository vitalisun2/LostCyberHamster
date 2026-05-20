using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Единый timing второго input для bot super roof-to-roof action и его planning travel.
    /// </summary>
    internal static class SuperJumpFromRoofOnRoofTiming
    {
        private const float UpgradeWindowSafetySeconds = 0.05f;

        public const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold - UpgradeWindowSafetySeconds;
        public const float UpgradeDelayTravel = UpgradeDelaySeconds * Consts.GameSpeedBase;
    }
}