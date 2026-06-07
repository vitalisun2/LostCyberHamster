using Assets.Scripts;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.StrategiesNew.SuperJumpOnFromRoof
{
    /// <summary>
    /// Единый timing второго input для super roof-to-road jump-on action и planning travel.
    /// </summary>
    internal static class SuperJumpOnFromRoofTiming
    {
        public const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;
        public const float UpgradeDelayTravel = UpgradeDelaySeconds * Consts.GameSpeedBase;
    }
}
