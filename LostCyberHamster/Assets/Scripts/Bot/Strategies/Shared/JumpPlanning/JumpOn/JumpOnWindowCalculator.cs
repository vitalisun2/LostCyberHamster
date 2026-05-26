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
        private const float RightEdgeToleranceRatio = 0.2f;

        public static bool TryCalculate(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            float jumpTravel,
            out JumpOnWindowModel window)
        {
            window = default;

            if (hamster == null || chain == null || chain.Count <= 0 || jumpTravel <= 0f)
                return false;

            ObstacleSnapshot targetObstacle = chain.FirstObstacle;
            if (targetObstacle.IsBottomLine != hamster.IsOnBottomLine
                || !ObstacleClassifier.CanJumpOnGroundObstacle(targetObstacle.ObstacleType))
            {
                return false;
            }

            if (!TryGetOpenWindow(
                    hamster,
                    targetObstacle,
                    jumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            float selectedFireShift = (firstFireShift + lastFireShift) * 0.5f;
            window = new JumpOnWindowModel(
                targetObstacle,
                chain.FirstIndex,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            float rightTolerance = hamster.Width * RightEdgeToleranceRatio;
            firstFireShift = targetObstacle.LeftX - jumpTravel - hamster.CenterX;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            float lastFireShiftBeforeOvershoot =
                targetObstacle.RightX + rightTolerance - jumpTravel - hamster.CenterX;
            float lastFireShiftBeforeGroundContact = targetObstacle.LeftX - hamster.HamsterRightX;
            lastFireShift = Math.Min(
                lastFireShiftBeforeOvershoot,
                lastFireShiftBeforeGroundContact);

            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return lastFireShift > 0f && firstFireShift < lastFireShift;
        }
    }
}
