using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Строит current-lane decision point.
    /// </summary>
    public sealed class DecisionPointDetectorNew
    {
        private readonly ObstacleChainBuilderNew _chainBuilder = new ObstacleChainBuilderNew();

        /// <summary>
        /// Пытается построить ближайшую role-based planning-ситуацию.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
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
                    out ObstacleChainNew chain))
            {
                decisionPoint = new DecisionPointNew(chain);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает index obstacle, с которого detector должен начать поиск point.
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
