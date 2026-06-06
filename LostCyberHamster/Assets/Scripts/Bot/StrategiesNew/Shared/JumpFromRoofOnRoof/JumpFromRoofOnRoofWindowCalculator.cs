using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Вычисляет fire-window для прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal static class JumpFromRoofOnRoofWindowCalculator
    {
        /// <summary>
        /// Пытается вычислить допустимое окно запуска roof-to-roof прыжка.
        /// </summary>
        public static bool TryCalculate(
            PlanningState planningState,
            ObstacleSnapshot lastRoof,
            ObstacleSnapshot targetRoof,
            ObstacleSnapshot runFromRoofBlocker,
            ObstacleSnapshot lastObstacleBeforeTargetRoof,
            float bigAliveCollisionPaddingRatio,
            JumpFromRoofOnRoofTravel travel,
            out float firstFireShift,
            out float lastFireShift,
            out float selectedFireShift)
        {
            // Инициализирует пустой результат.
            firstFireShift = 0f;
            lastFireShift = 0f;
            selectedFireShift = 0f;

            // Отсекает неполный вход.
            if (planningState == null || lastRoof == null || targetRoof == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            // Строит окно запуска как пересечение ограничений.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();

            firstFireShift = 0f;
            lastFireShift = float.PositiveInfinity;

            ApplyRoofRunLimit(hamster, lastRoof, fireWindowBoundaryMargin, ref lastFireShift);
            ApplyTargetRoofLandingLimit(
                hamster,
                targetRoof,
                travel,
                fireWindowBoundaryMargin,
                ref firstFireShift,
                ref lastFireShift);
            ApplyBigAliveFireWindowLimits(
                hamster,
                runFromRoofBlocker,
                lastObstacleBeforeTargetRoof,
                bigAliveCollisionPaddingRatio,
                fireWindowBoundaryMargin,
                ref firstFireShift,
                ref lastFireShift);

            // Выбирает середину итогового окна.
            if (firstFireShift >= lastFireShift)
                return false;

            selectedFireShift = (firstFireShift + lastFireShift) * 0.5f;
            return selectedFireShift > firstFireShift;
        }

        /// <summary>
        /// Уменьшает lastFireShift до последнего запуска до выхода из RoofRun.
        /// </summary>
        private static void ApplyRoofRunLimit(
            HamsterSnapshot hamster,
            ObstacleSnapshot lastRoof,
            float fireWindowBoundaryMargin,
            ref float lastFireShift)
        {
            // Считает последний fire shift до автоматического схода с крыши.
            float latestRoofRunFireShift =
                lastRoof.RightX
                + hamster.Width * RoofRunProjection.PassiveContinuationGapFactor
                - hamster.HamsterRightX;

            // Сужает правую границу окна.
            float latestBeforeRunFromRoof = latestRoofRunFireShift - fireWindowBoundaryMargin;
            if (latestBeforeRunFromRoof < lastFireShift)
                lastFireShift = latestBeforeRunFromRoof;
        }

        /// <summary>
        /// Сужает fire-window для пролета над bigAlive между крышами.
        /// </summary>
        private static void ApplyBigAliveFireWindowLimits(
            HamsterSnapshot hamster,
            ObstacleSnapshot firstObstacle,
            ObstacleSnapshot lastObstacle,
            float bigAliveCollisionPaddingRatio,
            float fireWindowBoundaryMargin,
            ref float firstFireShift,
            ref float lastFireShift)
        {
            // Ограничивает поздний запуск перед первым bigAlive.
            if (firstObstacle?.ObstacleType == ObstacleTypeEnum.bigAlive)
            {
                float latestBeforeFirstBigAlive =
                    firstObstacle.LeftX
                    - hamster.HamsterRightX
                    - fireWindowBoundaryMargin;

                if (latestBeforeFirstBigAlive < lastFireShift)
                    lastFireShift = latestBeforeFirstBigAlive;
            }

            // Применяет дополнительный padding для bigAlive.
            float padding = hamster.Width * bigAliveCollisionPaddingRatio;
            if (padding <= 0f)
                return;

            if (firstObstacle?.ObstacleType == ObstacleTypeEnum.bigAlive)
                lastFireShift -= padding;

            if (lastObstacle?.ObstacleType == ObstacleTypeEnum.bigAlive)
                firstFireShift += padding;
        }

        /// <summary>
        /// Сужает fire-window по достижимости посадки на target roof.
        /// </summary>
        private static void ApplyTargetRoofLandingLimit(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetRoof,
            JumpFromRoofOnRoofTravel travel,
            float fireWindowBoundaryMargin,
            ref float firstFireShift,
            ref float lastFireShift)
        {
            // Считает границы посадки на target roof.
            float firstLandingShift =
                targetRoof.LeftX
                - travel.RoofJumpTravel
                - hamster.HamsterRightX
                + fireWindowBoundaryMargin;

            float lastLandingShift =
                targetRoof.RightX
                - travel.RoofJumpTravel
                - hamster.HamsterLeftX
                - fireWindowBoundaryMargin;

            // Пересекает текущее окно с landing limits.
            if (firstLandingShift > firstFireShift)
                firstFireShift = firstLandingShift;

            if (firstFireShift < 0f)
                firstFireShift = 0f;

            if (lastLandingShift < lastFireShift)
                lastFireShift = lastLandingShift;
        }
    }
}
