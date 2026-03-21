using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Результат применения одного шага к состоянию планировщика.
    /// </summary>
    public class StepProjectionResult
    {
        public bool IsSafe;
        public PlannerState NextState;
        public List<int> ConsumedObjectIds = new List<int>();
        public string DebugReason;
    }
}
