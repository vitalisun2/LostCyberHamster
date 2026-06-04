using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOver
{
    /// <summary>
    /// Вычисляет chain и fire window для role-based ground jump-over.
    /// </summary>
    internal static class JumpOverChainCalculatorNew
    {
        /// <summary>
        /// Пытается вычислить безопасное окно запуска jump-over для role-based chain.
        /// </summary>
        public static bool TryCalculate(
            IJumpOverPolicy policy,
            HamsterSnapshot hamster,
            ObstacleChainNew chain,
            float jumpTravel,
            out JumpOverChainModel window)
        {
            // Проверяет входы и первый obstacle текущей ситуации.
            window = default;
            if (policy == null || hamster == null || chain == null || chain.Count <= 0)
                return false;

            ObstacleChainElementNew targetElement = chain.First;
            if (!targetElement.HasRole(ObstacleRole.BlockingThreat))
                return false;

            ObstacleSnapshot targetObstacle = targetElement.Obstacle;
            if (!policy.CanJumpOverObstacle(targetObstacle.ObstacleType))
                return false;

            // Инициализирует границы chain по первому obstacle.
            bool isBottomLine = targetObstacle.IsBottomLine;
            float chainLeftX = targetObstacle.LeftX;
            float chainRightX = targetObstacle.RightX;
            int firstObstacleIndex = targetElement.WorldIndex;
            int lastObstacleIndex = firstObstacleIndex;
            ObstacleSnapshot lastObstacle = targetObstacle;
            int obstacleCount = 1;

            if (!TryGetOpenWindow(
                    hamster,
                    chainLeftX,
                    chainRightX,
                    jumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Расширяет jump-over chain только через obstacles, которые этот policy реально перепрыгивает.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleChainElementNew element))
                    return false;

                ObstacleSnapshot obstacle = element.Obstacle;
                if (obstacle.IsBottomLine != isBottomLine)
                    return false;

                if (!element.HasRole(ObstacleRole.BlockingThreat)
                    || !policy.CanJumpOverObstacle(obstacle.ObstacleType))
                {
                    break;
                }

                float candidateChainRightX = obstacle.RightX > chainRightX ? obstacle.RightX : chainRightX;
                if (!TryGetOpenWindow(
                        hamster,
                        chainLeftX,
                        candidateChainRightX,
                        jumpTravel,
                        out float candidateFirstFireShift,
                        out float candidateLastFireShift))
                {
                    break;
                }

                chainRightX = candidateChainRightX;
                lastObstacleIndex = element.WorldIndex;
                lastObstacle = obstacle;
                obstacleCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Применяет padding для super-policy, обычный policy возвращает 0.
            ApplyBigAliveCollisionPadding(
                policy,
                hamster,
                targetObstacle,
                lastObstacle,
                ref firstFireShift,
                ref lastFireShift);

            if (firstFireShift >= lastFireShift)
                return false;

            float selectedFireShift = (firstFireShift + lastFireShift) * 0.5f;
            window = new JumpOverChainModel(
                firstObstacleIndex,
                lastObstacleIndex,
                obstacleCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Считает базовое окно запуска для текущих границ chain.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Находит геометрические границы окна относительно hamster.
            firstFireShift = chainRightX - hamster.HamsterLeftX - jumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = chainLeftX - hamster.HamsterRightX;

            // Сужает окно на runtime boundary margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Сужает окно с учетом collision padding для bigAlive.
        /// </summary>
        private static void ApplyBigAliveCollisionPadding(
            IJumpOverPolicy policy,
            HamsterSnapshot hamster,
            ObstacleSnapshot firstObstacle,
            ObstacleSnapshot lastObstacle,
            ref float firstFireShift,
            ref float lastFireShift)
        {
            float padding = hamster.Width * policy.BigAliveCollisionPaddingRatio;
            if (padding <= 0f)
                return;

            if (firstObstacle.ObstacleType == ObstacleTypeEnum.bigAlive)
                lastFireShift -= padding;

            if (lastObstacle.ObstacleType == ObstacleTypeEnum.bigAlive)
                firstFireShift += padding;
        }
    }
}
