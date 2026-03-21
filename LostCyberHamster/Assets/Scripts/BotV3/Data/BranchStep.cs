namespace Assets.Scripts.BotV3
{
    public enum BranchStepStatus { Ready, InProgress, Completed }

    /// <summary>
    /// Один шаг в ветви действий бота.
    /// </summary>
    public class BranchStep
    {
        public BotAction Action;
        public ObstacleInfo TargetObstacle;

        /// <summary>При какой дистанции до объекта выполнить действие.</summary>
        public float ExecuteAtDistance;

        public int EnergyCost;
        public string Reason;
        public BranchStepStatus Status;

        public BranchStep(
            BotAction action,
            ObstacleInfo targetObstacle,
            float executeAtDistance,
            int energyCost,
            string reason)
        {
            Action = action;
            TargetObstacle = targetObstacle;
            ExecuteAtDistance = executeAtDistance;
            EnergyCost = energyCost;
            Reason = reason;
            Status = BranchStepStatus.Ready;
        }
    }
}
