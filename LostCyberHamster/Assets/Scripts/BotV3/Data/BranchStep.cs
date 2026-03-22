namespace Assets.Scripts.BotV3
{
    public enum BranchStepStatus { Ready, InProgress, Completed }

    /// <summary>
    /// Один шаг в ветви действий бота.
    /// FireWorldShift — на сколько мир сдвинется от текущего snapshot до момента fire.
    /// CompletionWorldShift — на сколько мир сдвинется от текущего snapshot до завершения шага.
    /// </summary>
    public class BranchStep
    {
        public BotAction Action;
        public ObstacleInfo TargetObstacle;

        /// <summary>При какой дистанции до объекта выполнить действие.</summary>
        public float ExecuteAtDistance;

        /// <summary>Сдвиг мира от snapshot до момента fire.</summary>
        public float FireWorldShift;

        /// <summary>Сдвиг мира от snapshot до завершения шага (fire + transit).</summary>
        public float CompletionWorldShift;

        public int EnergyCost;
        public string Reason;
        public BranchStepStatus Status;

        public BranchStep(
            BotAction action,
            ObstacleInfo targetObstacle,
            float executeAtDistance,
            float fireWorldShift,
            float completionWorldShift,
            int energyCost,
            string reason)
        {
            Action = action;
            TargetObstacle = targetObstacle;
            ExecuteAtDistance = executeAtDistance;
            FireWorldShift = fireWorldShift;
            CompletionWorldShift = completionWorldShift;
            EnergyCost = energyCost;
            Reason = reason;
            Status = BranchStepStatus.Ready;
        }
    }
}
