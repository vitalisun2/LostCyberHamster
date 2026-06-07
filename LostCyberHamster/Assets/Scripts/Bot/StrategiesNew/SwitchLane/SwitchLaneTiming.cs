namespace Assets.Scripts.Bot.StrategiesNew.SwitchLane
{
    /// <summary>
    /// Хранит timing-константы и sampling-ratio для планирования смены линии.
    /// </summary>
    internal static class SwitchLaneTiming
    {
        /// <summary>
        /// Длительность визуального перестроения между линиями.
        /// </summary>
        public const float DecisionDuration = 0.45f;

        /// <summary>
        /// Дистанция мира за время визуального перестроения между линиями.
        /// </summary>
        public const float DecisionTravel = DecisionDuration * Assets.Scripts.Consts.GameSpeedBase;

        /// <summary>
        /// Дополнительная planning-дистанция после tap SwitchLane перед следующим действием.
        /// </summary>
        public const float PostFirePlanningTravel = 0f;

        /// <summary>
        /// Минимальный зазор до target obstacle в момент запуска SwitchLane.
        /// </summary>
        public const float ExecutionLeadDistance = 0.18f;

        /// <summary>
        /// Ratio для выбора ранней точки внутри safe-window SwitchLane.
        /// </summary>
        public const float EarlyWindowSelectionRatio = 0.05f;

        /// <summary>
        /// Ratio для выбора середины safe-window SwitchLane.
        /// </summary>
        public const float MidWindowSelectionRatio = 0.5f;
    }
}
