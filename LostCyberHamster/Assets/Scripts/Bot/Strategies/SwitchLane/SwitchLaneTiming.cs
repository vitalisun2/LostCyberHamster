namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Хранит timing-константы и параметры выбора окна для планирования смены линии.
    /// </summary>
    internal static class SwitchLaneTiming
    {
        /// <summary>
        /// Длительность визуального перестроения между линиями.
        /// </summary>
        public const float DecisionDuration = 0.45f;

        /// <summary>
        /// Количество кадров, за которое runtime гарантированно отдаёт управление следующему bot action.
        /// </summary>
        private const int RuntimeHandoffLatencyFrames = 2;

        /// <summary>
        /// Дополнительное время между окончанием animator transition и доступностью следующего bot action.
        /// </summary>
        public const float RuntimeHandoffLatencyDuration =
            RuntimeHandoffLatencyFrames / (float)Assets.Scripts.Consts.FPS;

        /// <summary>
        /// Дистанция мира до момента, когда следующий action можно безопасно запускать после SwitchLane.
        /// </summary>
        public const float DecisionTravel =
            (DecisionDuration + RuntimeHandoffLatencyDuration) * Assets.Scripts.Consts.GameSpeedBase;

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
    }
}
