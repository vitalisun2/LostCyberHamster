using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Builds role-based decision points for a selected focus lane.
    /// </summary>
    public sealed class DecisionPointDetectorNew
    {
        private readonly ObstacleChainBuilderNew _chainBuilder = new ObstacleChainBuilderNew();

        /// <summary>
        /// Tries to build the nearest role-based planning situation.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPointNew decisionPoint)
        {
            decisionPoint = null;
            if (planningState?.Hamster == null)
                return false;

            return TryDetect(
                planningState,
                worldSnapshot,
                planningState.IsOnBottomLine,
                out decisionPoint);
        }

        /// <summary>
        /// Tries to build the nearest role-based planning situation for the selected focus lane.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            out DecisionPointNew decisionPoint)
        {
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            int firstDetectionIndex = GetFirstDetectionIndex(planningState, worldSnapshot);
            if (_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    firstDetectionIndex,
                    focusBottomLine,
                    out ObstacleChainNew chain))
            {
                decisionPoint = new DecisionPointNew(chain);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the obstacle index where detection should start.
        /// </summary>
        private static int GetFirstDetectionIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            int defaultDetectionIndex = planningState.NextObstacleIndex;
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.IsOnRoof)
                return defaultDetectionIndex;

            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out _,
                    out int lastRoofIndex))
            {
                return defaultDetectionIndex;
            }

            int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
            return firstIndexAfterPassiveRoofs > defaultDetectionIndex
                ? firstIndexAfterPassiveRoofs
                : defaultDetectionIndex;
        }
    }
}
