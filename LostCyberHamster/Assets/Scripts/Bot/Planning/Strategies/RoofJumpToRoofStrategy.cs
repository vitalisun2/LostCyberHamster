using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofJumpToRoof: перепрыгивание на другую крышу (bigNotAlive, mediumNotAlive).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofJumpToRoofStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.RoofJumpToRoof;

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
