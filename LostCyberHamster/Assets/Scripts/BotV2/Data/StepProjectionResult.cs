using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Результат применения одного planner-шага к произвольному состоянию.
    /// </summary>
    public class StepProjectionResult
    {
        public bool IsSafe;
        public PlannerState NextState;
        public List<ObstacleInfo> CollectedObjects = new List<ObstacleInfo>();
        public List<int> ConsumedObjectIds = new List<int>();
        public int EnergyDelta;
        public string DebugReason;
    }
}