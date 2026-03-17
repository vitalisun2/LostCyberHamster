namespace Assets.Scripts.BotV2
{
    public enum ChainStepStatus { Ready, InProgress, Completed }

    /// <summary>
    /// Семантика шага для UI/дебага: что именно должен означать шаг,
    /// независимо от конкретной анимационной кривой.
    /// </summary>
    public enum StepSemantic
    {
        Unknown = 0,
        SwitchLane,
        JumpOver,
        SuperJumpOver,
        JumpOnBounce,
        JumpOnRoof
    }

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
        public StepSemantic Semantic;

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
            DecisionRank rank = DecisionRank.ThreatSafety,
            StepSemantic semantic = StepSemantic.Unknown)
        {
            Action = action;
            TargetObstacle = targetObstacle;
            ExecuteAtDistance = executeAtDistance;
            EnergyCost = energyCost;
            ProfitScore = profitScore;
            Rank = rank;
            Reason = reason;
            Semantic = semantic == StepSemantic.Unknown
                ? ResolveSemantic(action, targetObstacle)
                : semantic;
            Status = ChainStepStatus.Ready;
        }

        /// <summary>
        /// Единый компаратор приоритетов шагов: >0 означает a лучше b.
        /// Rank → ProfitScore → EnergyCost → ExecuteAtDistance.
        /// </summary>
        public static int ComparePriority(ChainStep a, ChainStep b)
        {
            if (a.Rank != b.Rank)
                return b.Rank.CompareTo(a.Rank);

            if (a.ProfitScore != b.ProfitScore)
                return a.ProfitScore.CompareTo(b.ProfitScore);

            if (a.EnergyCost != b.EnergyCost)
                return b.EnergyCost.CompareTo(a.EnergyCost);

            return a.ExecuteAtDistance.CompareTo(b.ExecuteAtDistance);
        }

        private static StepSemantic ResolveSemantic(BotAction action, ObstacleInfo target)
        {
            switch (action)
            {
                case BotAction.SwitchLane:
                    return StepSemantic.SwitchLane;

                case BotAction.SuperJump:
                    return StepSemantic.SuperJumpOver;

                case BotAction.Jump:
                    if (target.Type == Assets.Scripts.Common.Models.ObstacleTypeEnum.smallAlive &&
                        target.Category == ObjectCategory.Target)
                    {
                        return StepSemantic.JumpOnBounce;
                    }

                    return StepSemantic.JumpOver;

                default:
                    return StepSemantic.Unknown;
            }
        }
    }
}
