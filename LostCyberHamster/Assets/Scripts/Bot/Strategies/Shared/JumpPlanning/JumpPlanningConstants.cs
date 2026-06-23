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
