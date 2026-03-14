namespace Assets.Scripts.BotV2
{
    public enum ChainStepStatus { Ready, InProgress, Completed }

    /// <summary>
    /// Явный ранг приоритета решения: меньший ранг выбирается раньше.
    /// </summary>
    public enum DecisionRank
    {
        LifeCollectible = 0,
        Target = 1,
        OtherCollectible = 2,
        ThreatSafety = 3
    }

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
        public DecisionRank Rank;
        public string Reason;
        public ChainStepStatus Status;

        public ChainStep(
            BotAction action,
            ObstacleInfo targetObstacle,
            float executeAtDistance,
            int energyCost,
            string reason,
            int profitScore = 0,
            DecisionRank rank = DecisionRank.ThreatSafety)
        {
            Action = action;
            TargetObstacle = targetObstacle;
            ExecuteAtDistance = executeAtDistance;
            EnergyCost = energyCost;
            ProfitScore = profitScore;
            Rank = rank;
            Reason = reason;
            Status = ChainStepStatus.Ready;
        }
    }
}
