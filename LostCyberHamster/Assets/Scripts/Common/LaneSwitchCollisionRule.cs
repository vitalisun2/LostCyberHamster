using UnityEngine;

namespace Assets.Scripts.Common
{
    /// <summary>
    /// Задаёт допустимое перекрытие с уходящим препятствием при старте смены линии.
    /// </summary>
    public static class LaneSwitchCollisionRule
    {
        /// <summary>Допустимое перекрытие от ширины хомяка.</summary>
        public const float AllowedTrailingOverlapRatio = 0.3f;

        /// <summary>
        /// Возвращает сдвиг мира, после которого уходящее препятствие допускает смену линии.
        /// </summary>
        public static float GetDepartureReleaseShift(
            float hamsterLeftX,
            float hamsterRightX,
            float obstacleLeftX,
            float obstacleRightX)
        {
            float allowedOverlap = Mathf.Max(0f, hamsterRightX - hamsterLeftX)
                * AllowedTrailingOverlapRatio;

            return Mathf.Max(
                obstacleLeftX - hamsterLeftX,
                obstacleRightX - hamsterLeftX - allowedOverlap);
        }

        /// <summary>
        /// Проверяет, что правый край уходящего препятствия перекрывает не более 30% ширины хомяка.
        /// </summary>
        public static bool IsEligibleAtShiftStart(
            float hamsterLeftX,
            float hamsterRightX,
            float obstacleLeftX,
            float obstacleRightX)
        {
            return hamsterRightX > hamsterLeftX
                && obstacleRightX > hamsterLeftX
                && GetDepartureReleaseShift(
                    hamsterLeftX,
                    hamsterRightX,
                    obstacleLeftX,
                    obstacleRightX) <= 0f;
        }
    }
}
