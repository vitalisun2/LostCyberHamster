namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Хранит timing-константы смены линии.
    /// </summary>
    internal static class SwitchLaneTiming
    {
        public const float DecisionDuration = 0.45f;
        public const float DecisionTravel = DecisionDuration * Assets.Scripts.Consts.GameSpeedBase;
        public const float ExecutionLeadDistance = 0.18f;
        public const float InteriorSelectionRatio = 0.5f;
    }
}
