namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Один шаг в цепочке действий бота.
    /// </summary>
    public readonly struct ChainStep
    {
        public readonly BotAction Action;
        public readonly int TargetObstacleIndex;  // Индекс в массиве ObstacleInfo (-1 если нет конкретной цели)
        public readonly float ExecuteAtDistance;   // На каком расстоянии до объекта выполнить действие
        public readonly int EnergyCost;
        public readonly string Reason;

        public ChainStep(
            BotAction action,
            int targetObstacleIndex,
            float executeAtDistance,
            int energyCost,
            string reason)
        {
            Action = action;
            TargetObstacleIndex = targetObstacleIndex;
            ExecuteAtDistance = executeAtDistance;
            EnergyCost = energyCost;
            Reason = reason;
        }
    }
}
