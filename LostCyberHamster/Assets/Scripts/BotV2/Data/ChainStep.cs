namespace Assets.Scripts.BotV2
{
    public enum ChainStepStatus { Ready, InProgress, Completed }

    /// <summary>
    /// Один шаг в цепочке действий бота.
    /// </summary>
    public class ChainStep
    {
        public BotAction Action;
        public ObstacleInfo TargetObstacle;

        /// <summary>При какой дистанции до объекта выполнить действие.</summary>
        public float ExecuteAtDistance;

        public int EnergyCost;
        public int ProfitScore;
        public string Reason;
        public ChainStepStatus Status;

        public ChainStep(
            BotAction action,
            ObstacleInfo targetObstacle,
            float executeAtDistance,
            int energyCost,
            string reason,
            int profitScore = 0)
        {
            Action = action;
            TargetObstacle = targetObstacle;
            ExecuteAtDistance = executeAtDistance;
            EnergyCost = energyCost;
            ProfitScore = profitScore;
            Reason = reason;
            Status = ChainStepStatus.Ready;
        }
    }
}
