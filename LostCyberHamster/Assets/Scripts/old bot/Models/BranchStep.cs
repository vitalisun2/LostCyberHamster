namespace Assets.Scripts.Bot
{
    public enum BranchStepStatus { Ready, InProgress, Completed }

    /// <summary>
    /// Один шаг в ветви действий бота.
    /// FireWorldShift — на сколько мир сдвинется от текущего snapshot до момента fire.
    /// CompletionWorldShift — на сколько мир сдвинется от текущего snapshot до завершения шага.
    /// </summary>
    public class BranchStep
    {
        public BotAction Action { get; }
        public ObstacleInfo TargetObstacle { get; }

        /// <summary>При какой дистанции до объекта выполнить действие.</summary>
        public float ExecuteAtDistance { get; }

        /// <summary>Сдвиг мира от snapshot до момента fire.</summary>
        public float FireWorldShift { get; }

        /// <summary>Сдвиг мира от snapshot до завершения шага (fire + transit).</summary>
        public float CompletionWorldShift { get; }

        public int EnergyCost { get; }
        public string Reason { get; }
        public BranchStepStatus Status { get; private set; }

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

        public void MarkInProgress()
        {
            Status = BranchStepStatus.InProgress;
        }

        public void MarkCompleted()
        {
            Status = BranchStepStatus.Completed;
        }
    }
}
