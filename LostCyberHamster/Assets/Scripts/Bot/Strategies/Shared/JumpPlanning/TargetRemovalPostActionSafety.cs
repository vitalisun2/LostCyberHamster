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
        /// Возвращает true, если после действия с удалением target obstacle хомяк безопасно возвращается в Run.
        /// </summary>
        public static bool IsSafeAfterCompletion(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            int targetObstacleIndex,
            int targetObstacleInstanceId,
            float completionWorldShift,
            out string deadEndReason)
        {
            // Отсекает невалидные входные данные.
            deadEndReason = null;
            if (planningState == null
                || planningState.Hamster == null
                || projectedWorldSnapshot == null
                || completionWorldShift < 0f)
            {
                return false;
            }

            // Проверяет общий Run re-entry, игнорируя target, который action удаляет до completion.
            return RunReentryPostActionSafety.IsSafeAfterCompletion(
                planningState,
                projectedWorldSnapshot,
                completionWorldShift,
                targetObstacleIndex,
                targetObstacleInstanceId,
                "Небезопасное состояние после напрыгивания",
                out deadEndReason);
        }
    }
}
