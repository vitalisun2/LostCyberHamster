using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver
{
    /// <summary>
    /// Вычисляет chain и fire window для ground jump-over.
    /// </summary>
    internal static class JumpOverChainCalculator
    {
        public static bool TryCalculate(
            IJumpOverPolicy policy,
            HamsterSnapshot hamster,
            ObstacleChain chain,
            float jumpTravel,
            out JumpOverChainModel window)
        {
            window = default;

            if (policy == null || hamster == null || chain == null || chain.Count <= 0)
                return false;

            ObstacleSnapshot targetObstacle = chain.FirstObstacle;
            if (!policy.CanJumpOverObstacle(targetObstacle.ObstacleType))
                return false;

            bool isBottomLine = targetObstacle.IsBottomLine;
            float chainLeftX = targetObstacle.LeftX;
            float chainRightX = targetObstacle.RightX;
            int firstObstacleIndex = chain.FirstIndex;
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

            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleSnapshot obstacle, out int obstacleWorldIndex))
                    return false;

                if (obstacle.IsBottomLine != isBottomLine)
                    return false;

                if (!policy.CanJumpOverObstacle(obstacle.ObstacleType))
                    break;

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
                lastObstacleIndex = obstacleWorldIndex;
                lastObstacle = obstacle;
                obstacleCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

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

        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            firstFireShift = chainRightX - hamster.HamsterLeftX - jumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = chainLeftX - hamster.HamsterRightX;

            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return firstFireShift < lastFireShift;
        }

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
