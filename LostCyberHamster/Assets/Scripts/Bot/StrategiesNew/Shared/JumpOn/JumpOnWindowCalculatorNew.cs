using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOn
{
    /// <summary>
    /// Вычисляет fire-window для role-based ground jump-on target.
    /// </summary>
    internal static class JumpOnWindowCalculatorNew
    {
        private const float RightEdgeToleranceRatio = 0.2f;

        /// <summary>
        /// Вычисляет fire-window для заранее выбранного target внутри role-based chain.
        /// </summary>
        public static bool TryCalculate(
            HamsterSnapshot hamster,
            ObstacleChainNew chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            out JumpOnWindowModel window)
        {
            // Проверяет входы и target position внутри chain.
            window = default;
            if (hamster == null
                || chain == null
                || targetObstacle == null
                || chain.Count <= 0
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            if (!chain.TryGetAt(targetObstacleChainIndex, out ObstacleChainElementNew targetElement)
                || targetElement.Obstacle.InstanceId != targetObstacle.InstanceId
                || targetElement.WorldIndex != targetObstacleIndex)
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
                    out float lastFireShift))
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
            ObstacleChainNew chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Вычисляет левую границу по достижению target и очистке pre-target obstacles.
            float rightTolerance = hamster.Width * RightEdgeToleranceRatio;
            firstFireShift =
                targetObstacle.LeftX
                - travel.ResolveFireShiftOffset
                - travel.ResolveTravel
                - hamster.CenterX;
            ApplyPreTargetClearanceLimit(
                hamster,
                chain,
                targetObstacleChainIndex,
                travel,
                ref firstFireShift);
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

            // Сужает окно на общий safety margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return lastFireShift > 0f && firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Сдвигает левую границу до момента, когда pre-target obstacles не блокируют resolver.
        /// </summary>
        private static void ApplyPreTargetClearanceLimit(
            HamsterSnapshot hamster,
            ObstacleChainNew chain,
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
            ObstacleChainNew chain,
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
