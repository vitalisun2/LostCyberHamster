using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofJumpDown: спрыгивание с крыши на дорогу (нет препятствий впереди).
    /// Skeleton реализация — пока возвращает "not implemented".
    /// </summary>
    public class RoofJumpDownStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.RoofJumpDown;

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
