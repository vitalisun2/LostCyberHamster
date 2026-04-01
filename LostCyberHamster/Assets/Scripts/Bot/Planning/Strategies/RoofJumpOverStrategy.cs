using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofJumpOver: перепрыгивание на крыше (smallNotAliveRoadAndRoof on roof).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofJumpOverStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.RoofJumpOver;

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
