using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия SuperJumpOnRoof: суперпрыжком запрыгивание на крышу (bigNotAlive, mediumNotAlive).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class SuperJumpOnRoofStrategy : IActionStrategy
    {
        private readonly float _superJumpLandingOffset;

        public SuperJumpOnRoofStrategy(float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback)
        {
            _superJumpLandingOffset = superJumpLandingOffset;
        }

        public BotAction Action => BotAction.SuperJumpOnRoof;

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
