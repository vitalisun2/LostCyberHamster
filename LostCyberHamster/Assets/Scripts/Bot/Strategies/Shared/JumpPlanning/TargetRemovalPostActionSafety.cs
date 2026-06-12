using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет Run-состояние после действия, которое уничтожает target obstacle.
    /// </summary>
    internal static class TargetRemovalPostActionSafety
    {
        /// <summary>
        /// Допуск для проверки X-overlap после завершения action.
        /// </summary>
        private const float OverlapEpsilon = 0.0001f;

        /// <summary>
        /// Дистанция, которую re-entry в Run должен выдержать до следующего управляемого jump-window.
        /// </summary>
        private const float ReentryGuardTravel = JumpPlanningConstants.FireWindowBoundaryMargin;

        /// <summary>
        /// Возвращает true, если после полного действия хомяк безопасно возвращается в Run.
        /// </summary>
        public static bool IsSafeAfterCompletion(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            int targetObstacleIndex,
            int targetObstacleInstanceId,
            float completionWorldShift)
        {
            // Отсекает невалидные входные данные.
            if (planningState == null
                || planningState.Hamster == null
                || projectedWorldSnapshot == null
                || completionWorldShift < 0f)
            {
                return false;
            }

            // Проверяет все будущие ground-угрозы после завершения action.
            HamsterSnapshot hamster = planningState.Hamster;
            for (int obstacleIndex = 0;
                 obstacleIndex < projectedWorldSnapshot.Obstacles.Count;
                 obstacleIndex++)
            {
                // Пропускает уничтожаемый target.
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (IsRemovedTarget(obstacle, obstacleIndex, targetObstacleIndex, targetObstacleInstanceId))
                    continue;

                // Пропускает другую линию.
                if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                // Пропускает безопасный ground-contact.
                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                // Проецирует obstacle в момент возврата в Run.
                float projectedLeftX = obstacle.LeftX - completionWorldShift;
                float projectedRightX = obstacle.RightX - completionWorldShift;

                // Запрещает невылетевший pre-target blocker.
                if (obstacleIndex < targetObstacleIndex
                    && projectedRightX > hamster.HamsterLeftX + OverlapEpsilon)
                {
                    return false;
                }

                // Проверяет re-entry интервал, а не только точку completion.
                if (IsSafeThroughoutReentryGuard(hamster, projectedLeftX, projectedRightX))
                    continue;

                // Фиксирует небезопасный re-entry в Run.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверяет, является ли obstacle target-ом, который действие уничтожит до Run-состояния.
        /// </summary>
        private static bool IsRemovedTarget(
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            int targetObstacleIndex,
            int targetObstacleInstanceId)
        {
            return obstacleIndex == targetObstacleIndex
                || obstacle.InstanceId == targetObstacleInstanceId;
        }

        /// <summary>
        /// Возвращает true, если obstacle не пересекает хомяка до первого безопасного re-entry окна.
        /// </summary>
        private static bool IsSafeThroughoutReentryGuard(
            HamsterSnapshot hamster,
            float projectedLeftX,
            float projectedRightX)
        {
            // Пропускает obstacle, который уже полностью позади хомяка.
            if (projectedRightX <= hamster.HamsterLeftX + OverlapEpsilon)
                return true;

            // Пропускает obstacle, который останется впереди весь guard-интервал.
            float guardEndLeftX = projectedLeftX - ReentryGuardTravel;
            return guardEndLeftX >= hamster.HamsterRightX - OverlapEpsilon;
        }
    }
}
