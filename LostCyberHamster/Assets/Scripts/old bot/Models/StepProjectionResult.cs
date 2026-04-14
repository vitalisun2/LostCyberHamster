namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Результат применения одного шага к состоянию планировщика.
    /// </summary>
    public class StepProjectionResult
    {
        public bool IsSafe;
        public PlannerState NextState;
        public string DebugReason;
    }
}
