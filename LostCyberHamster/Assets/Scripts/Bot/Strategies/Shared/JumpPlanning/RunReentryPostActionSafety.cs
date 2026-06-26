using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет общий post-action invariant для действий, которые завершаются возвратом хомяка в Run.
    /// </summary>
    internal static class RunReentryPostActionSafety
    {
        /// <summary>
        /// Допуск для проверки X-overlap после завершения action.
        /// </summary>
        private const float OverlapEpsilon = 0.0001f;

        /// <summary>
        /// Дистанция, которую re-entry в Run должен выдержать до следующего управляемого jump-window.
        /// </summary>
        private const float ReentryGuardTravel = JumpPlanningConstants.PostActionReentryGuardTravel;

        /// <summary>
        /// Возвращает true, если action без удаляемого target безопасно переводит хомяка обратно в Run.
        /// </summary>
        public static bool IsSafeAfterCompletion(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            float completionWorldShift,
            string unsafeReasonPrefix,
            out string deadEndReason)
        {
            return IsSafeAfterCompletion(
                planningState,
                projectedWorldSnapshot,
                completionWorldShift,
                ignoredObstacleIndex: -1,
                ignoredObstacleInstanceId: null,
                unsafeReasonPrefix,
                out deadEndReason);
        }

        /// <summary>
        /// Возвращает true, если action с уже удаленным target безопасно переводит хомяка обратно в Run.
        /// </summary>
        public static bool IsSafeAfterCompletion(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            float completionWorldShift,
            int ignoredObstacleIndex,
            int? ignoredObstacleInstanceId,
            string unsafeReasonPrefix,
            out string deadEndReason)
        {
            deadEndReason = null;
            if (planningState == null
                || planningState.Hamster == null
                || projectedWorldSnapshot == null
                || completionWorldShift < 0f)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            string reasonPrefix = string.IsNullOrEmpty(unsafeReasonPrefix)
                ? "Небезопасное состояние после действия"
                : unsafeReasonPrefix;

            // Проверяет только active ground threats на линии, где action завершится состоянием Run.
            for (int obstacleIndex = 0;
                 obstacleIndex < projectedWorldSnapshot.Obstacles.Count;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (IsIgnoredObstacle(obstacle, obstacleIndex, ignoredObstacleIndex, ignoredObstacleInstanceId))
                    continue;

                if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                float projectedLeftX = obstacle.LeftX - completionWorldShift;
                float projectedRightX = obstacle.RightX - completionWorldShift;

                // Запрещает pre-target blocker, который action не удаляет и который остается перед хомяком.
                if (obstacleIndex < ignoredObstacleIndex
                    && projectedRightX > hamster.HamsterLeftX + OverlapEpsilon)
                {
                    deadEndReason = $"{reasonPrefix}: перед target остается препятствие в зоне хомяка.";
                    return false;
                }

                // Проверяет не только точку completion, но и короткий участок до следующего action window.
                if (IsSafeThroughoutReentryGuard(hamster, projectedLeftX, projectedRightX))
                    continue;

                deadEndReason = $"{reasonPrefix}: после возврата в Run хомяк пересекает следующее опасное препятствие.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверяет, является ли obstacle target-ом, который уже удален action к моменту Run re-entry.
        /// </summary>
        private static bool IsIgnoredObstacle(
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            int ignoredObstacleIndex,
            int? ignoredObstacleInstanceId)
        {
            return obstacleIndex == ignoredObstacleIndex
                || (ignoredObstacleInstanceId.HasValue
                    && obstacle.InstanceId == ignoredObstacleInstanceId.Value);
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
