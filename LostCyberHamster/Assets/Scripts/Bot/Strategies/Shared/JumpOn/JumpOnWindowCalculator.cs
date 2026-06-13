using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Вычисляет fire-window для role-based ground jump-on target.
    /// </summary>
    internal static class JumpOnWindowCalculator
    {
        private const float RightEdgeToleranceRatio = 0.2f;

        /// <summary>
        /// Вычисляет fire-window для заранее выбранного target внутри role-based chain.
        /// </summary>
        public static bool TryCalculate(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            out JumpOnWindowModel window,
            out string deadEndReason)
        {
            // Проверяет входы.
            window = default;
            deadEndReason = null;
            if (hamster == null
                || chain == null
                || targetObstacle == null
                || chain.Count <= 0
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            // Вычисляет открытые границы окна.
            if (!TryGetOpenWindow(
                    hamster,
                    chain,
                    targetObstacle,
                    targetObstacleChainIndex,
                    travel,
                    out float firstFireShift,
                    out float lastFireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Собирает модель окна.
            float selectedFireShift = (firstFireShift + lastFireShift) * 0.5f;
            window = new JumpOnWindowModel(
                targetObstacle,
                targetObstacleIndex,
                targetObstacleChainIndex,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Вычисляет открытые границы запуска между недолётом, перелётом, pre-target clearance и ground-contact.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            out float firstFireShift,
            out float lastFireShift,
            out string deadEndReason)
        {
            // Вычисляет левую границу по достижению target и очистке pre-target obstacles.
            deadEndReason = null;
            float rightTolerance = hamster.Width * RightEdgeToleranceRatio;
            firstFireShift =
                targetObstacle.LeftX
                - travel.ResolveFireShiftOffset
                - travel.ResolveTravel
                - hamster.CenterX;
            float firstFireShiftBeforePreTargetClearance = firstFireShift;
            ApplyPreTargetClearanceLimit(
                hamster,
                chain,
                targetObstacleChainIndex,
                travel,
                ref firstFireShift);
            bool preTargetClearanceMovedWindow = firstFireShift > firstFireShiftBeforePreTargetClearance;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Вычисляет правую границу по перелёту и ground-contact.
            float lastFireShiftBeforeOvershoot =
                targetObstacle.RightX
                + rightTolerance
                - travel.ResolveFireShiftOffset
                - travel.ResolveTravel
                - hamster.CenterX;
            float lastFireShiftBeforeGroundContact =
                GetChainLeftEdgeBeforeTarget(chain, targetObstacleChainIndex) - hamster.HamsterRightX;
            lastFireShift = Math.Min(
                lastFireShiftBeforeOvershoot,
                lastFireShiftBeforeGroundContact);

            if (lastFireShift <= 0f || firstFireShift >= lastFireShift)
            {
                deadEndReason = BuildRawWindowDeadEndReason(
                    preTargetClearanceMovedWindow,
                    lastFireShiftBeforeGroundContact,
                    lastFireShiftBeforeOvershoot);
                return false;
            }

            // Сужает окно на общий safety margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;
            if (firstFireShift >= lastFireShift)
            {
                deadEndReason = "Safety margin не оставил безопасного окна для напрыгивания.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Выбирает короткую причину схлопывания raw-окна jump-on.
        /// </summary>
        private static string BuildRawWindowDeadEndReason(
            bool preTargetClearanceMovedWindow,
            float lastFireShiftBeforeGroundContact,
            float lastFireShiftBeforeOvershoot)
        {
            if (preTargetClearanceMovedWindow)
                return "Нет безопасного окна для напрыгивания: препятствие перед target закрывает траекторию.";

            if (lastFireShiftBeforeGroundContact <= lastFireShiftBeforeOvershoot)
                return "Нет безопасного окна для напрыгивания: до target остается опасный контакт с препятствием.";

            return "Нет безопасного окна для напрыгивания: target не пересекается с допустимой траекторией прыжка.";
        }

        /// <summary>
        /// Сдвигает левую границу до момента, когда pre-target obstacles не блокируют resolver.
        /// </summary>
        private static void ApplyPreTargetClearanceLimit(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            ref float firstFireShift)
        {
            // Проверяет препятствия до target.
            for (int chainIndex = 0; chainIndex < targetObstacleChainIndex; chainIndex++)
            {
                ObstacleSnapshot obstacle = chain.Elements[chainIndex].Obstacle;
                float earliestAfterObstacle =
                    obstacle.RightX
                    - travel.ResolveFireShiftOffset
                    - travel.ResolveTravel
                    - hamster.HamsterLeftX;

                if (earliestAfterObstacle > firstFireShift)
                    firstFireShift = earliestAfterObstacle;
            }
        }

        /// <summary>
        /// Возвращает самый ранний left edge цепочки до target включительно.
        /// </summary>
        private static float GetChainLeftEdgeBeforeTarget(
            ObstacleChain chain,
            int targetObstacleChainIndex)
        {
            // Инициализирует левый край target-ом.
            float chainLeftEdge = chain.Elements[targetObstacleChainIndex].Obstacle.LeftX;

            // Проверяет obstacles до target.
            for (int chainIndex = 0; chainIndex < targetObstacleChainIndex; chainIndex++)
            {
                ObstacleSnapshot obstacle = chain.Elements[chainIndex].Obstacle;
                if (obstacle.LeftX < chainLeftEdge)
                    chainLeftEdge = obstacle.LeftX;
            }

            return chainLeftEdge;
        }
    }
}
