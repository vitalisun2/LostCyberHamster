using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит required blocking-chain для опасного occupant на текущем passive roof path.
    /// </summary>
    internal sealed class RoofOccupantHazardChainBuilder : IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить roof occupant hazard decision point.
        /// </summary>
        public bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (!context.HasValidInput || !context.Hamster.IsOnRoof)
                return false;

            // Ищет первый опасный occupant на текущем passive roof path.
            if (!TryFindRoofOccupantHazard(
                    context,
                    out int roofOccupantHazardIndex))
            {
                return false;
            }

            // Строит blocking-chain от найденного roof occupant.
            if (!ThreatChainCollector.TryBuildChainFromIndex(
                    context.PlanningState,
                    context.WorldSnapshot,
                    roofOccupantHazardIndex,
                    context.PlanningState.IsOnBottomLine,
                    out ObstacleChain chain))
            {
                return false;
            }

            decisionPoint = new DecisionPoint(
                chain,
                DecisionPointKind.BlockingThreat,
                isDecisionRequired: true);
            return true;
        }

        /// <summary>
        /// Пытается найти первый roof occupant hazard на текущей цепочке крыш.
        /// </summary>
        private static bool TryFindRoofOccupantHazard(
            DecisionPointBuildContext context,
            out int firstHazardIndex)
        {
            // Ищет первый опасный occupant на текущем passive roof path.
            firstHazardIndex = -1;
            for (int obstacleIndex = 0; obstacleIndex < context.WorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = context.WorldSnapshot.Obstacles[obstacleIndex];
                if (!RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                        context.PlanningState,
                        context.WorldSnapshot,
                        obstacle,
                        out _,
                        out _))
                {
                    continue;
                }

                firstHazardIndex = obstacleIndex;
                return true;
            }

            return false;
        }
    }
}
