using Assets.Scripts;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Хранит общие численные параметры планирования jump fire-windows.
    /// </summary>
    internal static class JumpPlanningConstants
    {
        /// <summary>
        /// Небольшой геометрический отступ от обеих границ fire-window для jump-like действий.
        /// </summary>
        public const float FireWindowBoundaryMargin = 0.1f;

        /// <summary>
        /// Количество кадров, за которое runtime гарантированно отдаёт управление следующему bot action.
        /// </summary>
        public const int RuntimeHandoffLatencyFrames = 2;

        /// <summary>
        /// Дополнительное время между завершением action и доступностью следующего bot action.
        /// </summary>
        public const float RuntimeHandoffLatencyDuration =
            RuntimeHandoffLatencyFrames / (float)Consts.FPS;

        /// <summary>
        /// Дистанция мира, проходящая за runtime handoff latency.
        /// </summary>
        public const float RuntimeHandoffLatencyTravel =
            RuntimeHandoffLatencyDuration * Consts.GameSpeedBase;

        /// <summary>
        /// Минимальный безопасный зазор после возврата в Run до ближайшей ground-угрозы.
        /// </summary>
        public const float PostActionReentryGuardTravel =
            RuntimeHandoffLatencyTravel + FireWindowBoundaryMargin;

        /// <summary>
        /// Возвращает геометрический отступ fire-window.
        /// </summary>
        public static float GetEffectiveFireWindowBoundaryMargin()
        {
            return FireWindowBoundaryMargin;
        }

        /// <summary>
        /// Возвращает отступ fire-window для заданного time scale.
        /// </summary>
        public static float GetEffectiveFireWindowBoundaryMargin(float timeScale)
        {
            return FireWindowBoundaryMargin;
        }
    }
}
