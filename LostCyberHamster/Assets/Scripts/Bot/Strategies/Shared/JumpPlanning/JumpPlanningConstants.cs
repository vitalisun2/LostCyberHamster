using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Хранит общие численные параметры планирования jump fire-windows.
    /// </summary>
    internal static class JumpPlanningConstants
    {
        /// <summary>
        /// Базовый внутренний отступ от обеих границ fire-window для jump-like действий.
        /// </summary>
        public const float FireWindowBoundaryMargin = 0.1f;

        /// <summary>
        /// Возвращает runtime-отступ fire-window с учетом ускорения времени.
        /// </summary>
        public static float GetEffectiveFireWindowBoundaryMargin()
        {
            return GetEffectiveFireWindowBoundaryMargin(Time.timeScale);
        }

        /// <summary>
        /// Возвращает отступ fire-window для заданного time scale.
        /// </summary>
        public static float GetEffectiveFireWindowBoundaryMargin(float timeScale)
        {
            if (timeScale <= 1f)
                return FireWindowBoundaryMargin;

            return FireWindowBoundaryMargin * timeScale;
        }
    }
}
