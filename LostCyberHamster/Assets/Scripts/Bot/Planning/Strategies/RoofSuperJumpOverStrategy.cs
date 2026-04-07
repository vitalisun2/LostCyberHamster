using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofSuperJumpOver: суперпрыжком перепрыгивание на крыше (smallNotAliveRoadAndRoof on roof).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofSuperJumpOverStrategy : IActionStrategy
    {
        private readonly float _superJumpLandingOffset;

        public RoofSuperJumpOverStrategy(float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback)
        {
            _superJumpLandingOffset = superJumpLandingOffset;
        }

        public BotAction Action => BotAction.RoofSuperJumpOver;

        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;
            rejectReason = "not implemented";
            return false;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            return new StepProjectionResult
            {
                IsSafe = false,
                DebugReason = "not implemented"
            };
        }
    }
}
