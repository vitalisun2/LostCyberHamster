using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies
{
    /// <summary>
    /// Рассчитывает окно fire shift для посадки на roof obstacle.
    /// </summary>
    internal sealed class RoofLandingSearchWindowPolicy : IJumpSearchWindowPolicy
    {
        public bool TryGetSearchWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (targetObstacle, nameof(targetObstacle)));

            HamsterSnapshot hamster = planningState.Hamster;
            firstFireShift = targetObstacle.LeftX - actionTravel - hamster.HamsterRightX;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            float lastRoofOverlapFireShift = targetObstacle.RightX - hamster.HamsterLeftX;
            float latestBeforeGroundContactFireShift = targetObstacle.LeftX - hamster.HamsterRightX;

            lastFireShift = global::System.Math.Min(lastRoofOverlapFireShift, latestBeforeGroundContactFireShift);
            return lastFireShift > 0f && firstFireShift <= lastFireShift;
        }
    }
}
