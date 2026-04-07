using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofSuperJumpDown: суперпрыжком спрыгивание с крыши (нет препятствий).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofSuperJumpDownStrategy : IActionStrategy
    {
        private readonly float _superJumpLandingOffset;

        public RoofSuperJumpDownStrategy(float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback)
        {
            _superJumpLandingOffset = superJumpLandingOffset;
        }

        public BotAction Action => BotAction.RoofSuperJumpDown;

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
