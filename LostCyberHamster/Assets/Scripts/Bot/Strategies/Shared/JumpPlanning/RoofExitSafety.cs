using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет safety-интервал пассивного схода с крыши и возврата в Run.
    /// </summary>
    internal static class RoofExitSafety
    {
        /// <summary>
        /// Допуск для проверки X-overlap после завершения action.
        /// </summary>
        private const float OverlapEpsilon = 0.0001f;

        /// <summary>
        /// Минимальный безопасный участок после возврата в Run до следующего управляемого окна.
        /// </summary>
        private const float RunReentryGuardTravel = JumpPlanningConstants.PostActionReentryGuardTravel;

        /// <summary>
        /// Возвращает true, если пассивный сход не приводит к runtime-damage контакту.
        /// </summary>
        public static bool IsSafeDuringRunFromRoof(
            HamsterSnapshot hamster,
            WorldSnapshot worldSnapshot,
            bool targetBottomLine,
            float startShift,
            float completionWorldShift,
            out string deadEndReason)
        {
            deadEndReason = null;
            if (hamster == null || worldSnapshot == null || completionWorldShift < startShift)
                return false;

            // Проверяет ground-угрозы на линии, на которую хомяк возвращается после крыши.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                // До completion опасны только препятствия без roof-поверхности.
                if (DamagesDuringRunFromRoof(obstacle)
                    && OverlapsHamsterDuringShift(hamster, obstacle, startShift, completionWorldShift))
                {
                    deadEndReason = "Нет безопасного окна для пассивного схода с крыши: интервал RunFromRoof пересекает дорожное опасное препятствие.";
                    return false;
                }

                // После completion проверяется уже обычный Run re-entry с guard-дистанцией.
                if (!IsObstacleSafeAfterRunReentry(hamster, obstacle, completionWorldShift))
                {
                    deadEndReason = "Нет безопасного окна для пассивного схода с крыши: после перехода в Run хомяк пересекает опасное препятствие.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Возвращает true, если после action хомяк безопасно возвращается в Run на заданной линии.
        /// </summary>
        public static bool IsSafeAfterRunReentry(
            HamsterSnapshot hamster,
            WorldSnapshot worldSnapshot,
            bool targetBottomLine,
            float completionWorldShift,
            out string deadEndReason)
        {
            deadEndReason = null;
            if (hamster == null || worldSnapshot == null || completionWorldShift < 0f)
                return false;

            // Проверяет ground-угрозы на линии, где action завершится состоянием Run.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (IsObstacleSafeAfterRunReentry(hamster, obstacle, completionWorldShift))
                    continue;

                deadEndReason = "Небезопасный возврат в Run: после завершения действия хомяк пересекает опасное препятствие.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Возвращает true, если obstacle может дамажить до завершения RunFromRoof.
        /// </summary>
        private static bool DamagesDuringRunFromRoof(ObstacleSnapshot obstacle)
        {
            return !ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
        }

        /// <summary>
        /// Возвращает true, если obstacle пересекает хомяка в заданном shift-интервале.
        /// </summary>
        private static bool OverlapsHamsterDuringShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            float startShift,
            float endShift)
        {
            float firstOverlapShift = obstacle.LeftX - hamster.HamsterRightX;
            float lastOverlapShift = obstacle.RightX - hamster.HamsterLeftX;

            return firstOverlapShift <= endShift
                && lastOverlapShift >= startShift;
        }

        /// <summary>
        /// Возвращает true, если после перехода в Run obstacle не пересекает хомяка до первого безопасного окна.
        /// </summary>
        private static bool IsObstacleSafeAfterRunReentry(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            float completionWorldShift)
        {
            float projectedLeftX = obstacle.LeftX - completionWorldShift;
            float projectedRightX = obstacle.RightX - completionWorldShift;

            if (projectedRightX <= hamster.HamsterLeftX + OverlapEpsilon)
                return true;

            float guardEndLeftX = projectedLeftX - RunReentryGuardTravel;
            return guardEndLeftX >= hamster.HamsterRightX - OverlapEpsilon;
        }
    }
}
