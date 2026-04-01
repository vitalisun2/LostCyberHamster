using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofSuperJumpOnTarget: суперпрыжком напрыгивание на alive с крыши (smallAlive, bigAlive).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofSuperJumpOnTargetStrategy : IActionStrategy
    {
        private readonly float _superJumpLandingOffset;

        public RoofSuperJumpOnTargetStrategy(float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback)
        {
            _superJumpLandingOffset = superJumpLandingOffset;
        }

        public BotAction Action => BotAction.RoofSuperJumpOnTarget;

        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
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
