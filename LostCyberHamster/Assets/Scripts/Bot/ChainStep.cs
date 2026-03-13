namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Статус выполнения шага плана.
    /// </summary>
    public enum ChainStepStatus
    {
        Ready,
        InProgress,
        Completed
    }

    /// <summary>
    /// Один шаг в цепочке действий бота.
    /// Изменён с struct на class для поддержки мутабельного поля Status.
    /// </summary>
    public class ChainStep
    {
        public BotAction Action;

        /// <summary>Индекс в массиве ObstacleInfo (-1 если нет конкретной цели). Сохранён для обратной совместимости.</summary>
        public int TargetObstacleIndex;

        /// <summary>Прямая ссылка на целевой объект (заполняется в новом pipeline, иначе null).</summary>
        public ObstacleInfo? TargetObstacle;

        /// <summary>На каком расстоянии до объекта выполнить действие.</summary>
        public float ExecuteAtDistance;

        public int EnergyCost;
        public string Reason;
        public ChainStepStatus Status;

        public ChainStep(
            BotAction action,
            int targetObstacleIndex,
            float executeAtDistance,
            int energyCost,
            string reason)
        {
            Action = action;
            TargetObstacleIndex = targetObstacleIndex;
            TargetObstacle = null;
            ExecuteAtDistance = executeAtDistance;
            EnergyCost = energyCost;
            Reason = reason;
            Status = ChainStepStatus.Ready;
        }
    }
}
