using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofJumpOnTarget: напрыгивание на alive с крыши (smallAlive, bigAlive).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofJumpOnTargetStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.RoofJumpOnTarget;

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
