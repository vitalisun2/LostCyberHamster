namespace Assets.Scripts.Bot.PlanState
{
    /// <summary>
    /// Описывает диапазон live X-координат trigger-obstacle, в котором action можно запускать.
    /// </summary>
    public readonly struct ActionTriggerWindow
    {
        public ActionTriggerWindow(float earliestTriggerX, float latestTriggerX)
        {
            EarliestTriggerX = earliestTriggerX;
            LatestTriggerX = latestTriggerX;
        }

        /// <summary>
        /// Larger obstacle-left X where the fire window opens.
        /// </summary>
        public float EarliestTriggerX { get; }

        /// <summary>
        /// Smaller obstacle-left X where the fire window closes.
        /// </summary>
        public float LatestTriggerX { get; }

        public bool IsValid => EarliestTriggerX >= LatestTriggerX;

        public float Width => IsValid ? EarliestTriggerX - LatestTriggerX : 0f;

        public bool Contains(float obstacleLeftX)
        {
            return IsValid
                && obstacleLeftX <= EarliestTriggerX
                && obstacleLeftX >= LatestTriggerX;
        }

        public bool WasCrossed(float previousObstacleLeftX, float currentObstacleLeftX)
        {
            return IsValid
                && previousObstacleLeftX > EarliestTriggerX
                && currentObstacleLeftX < LatestTriggerX;
        }

        public static ActionTriggerWindow FromSelectedTrigger(
            float selectedTriggerX,
            float selectedFireShift,
            float firstFireShift,
            float lastFireShift)
        {
            float triggerObstacleLeftX = selectedTriggerX + selectedFireShift;
            return new ActionTriggerWindow(
                triggerObstacleLeftX - firstFireShift,
                triggerObstacleLeftX - lastFireShift);
        }
    }
}
