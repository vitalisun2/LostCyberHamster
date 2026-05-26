using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Вычисляет fire-window для ground jump-on по runtime-условию smallAlive.
    /// </summary>
    internal static class JumpOnWindowCalculator
    {
        /// <summary>
        /// Допуск к правому краю target относительно ширины хомяка.
        /// </summary>
        private const float RightEdgeToleranceRatio = 0.2f;

        /// <summary>
        /// Вычисляет fire-window для первого ground jump-on target в chain.
        /// </summary>
        public static bool TryCalculate(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            float resolveTravel,
            out JumpOnWindowModel window)
        {
            // Инициализирует результат.
            window = default;

            // Проверяет входные данные.
            if (hamster == null || chain == null || chain.Count <= 0 || resolveTravel <= 0f)
                return false;

            // Ищет target для напрыгивания.
            if (!chain.TryFindFirstGroundJumpOnTarget(
                    hamster.IsOnBottomLine,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex))
            {
                return false;
            }

            // Вычисляет открытые границы окна.
            if (!TryGetOpenWindow(
                    hamster,
                    chain,
                    targetObstacle,
                    targetObstacleChainIndex,
                    resolveTravel,
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
        /// Вычисляет открытые границы запуска между недолётом, перелётом и ground-contact.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleChainIndex,
            float resolveTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Вычисляет левую границу по достижению target.
            float rightTolerance = hamster.Width * RightEdgeToleranceRatio;
            firstFireShift = targetObstacle.LeftX - resolveTravel - hamster.CenterX;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Вычисляет правую границу по перелёту и ground-contact.
            float lastFireShiftBeforeOvershoot =
                targetObstacle.RightX + rightTolerance - resolveTravel - hamster.CenterX;
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

            // Проверяет, что окно осталось открытым.
            return lastFireShift > 0f && firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Возвращает самый ранний left edge цепочки до target включительно.
        /// </summary>
        private static float GetChainLeftEdgeBeforeTarget(
            ObstacleChain chain,
            int targetObstacleChainIndex)
        {
            // Инициализирует левый край target-ом.
            float chainLeftEdge = chain.Obstacles[targetObstacleChainIndex].LeftX;

            // Проверяет obstacles до target.
            for (int chainIndex = 0; chainIndex < targetObstacleChainIndex; chainIndex++)
            {
                ObstacleSnapshot obstacle = chain.Obstacles[chainIndex];
                if (obstacle.LeftX < chainLeftEdge)
                    chainLeftEdge = obstacle.LeftX;
            }

            // Возвращает самый ранний край.
            return chainLeftEdge;
        }
    }
}
