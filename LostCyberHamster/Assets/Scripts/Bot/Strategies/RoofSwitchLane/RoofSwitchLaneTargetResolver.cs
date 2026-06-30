using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Определяет target context для смены линии между крышами.
    /// </summary>
    internal sealed class RoofSwitchLaneTargetResolver
    {
        /// <summary>
        /// Определяет target context для defensive или reward roof switch-lane сценария.
        /// </summary>
        public bool TryResolve(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out RoofSwitchLaneTarget target)
        {
            // Инициализирует результат.
            target = default;

            // Проверяет входные данные.
            HamsterSnapshot hamster = planningState?.Hamster;
            ObstacleChain chain = decisionPoint?.Chain;
            if (hamster == null || chain == null)
                return false;

            // Выбирает сценарий по линии decision chain.
            bool chainIsCurrentLane = chain.First.IsBottomLine == hamster.IsOnBottomLine;
            return chainIsCurrentLane
                ? TryResolveDefensiveTarget(hamster, chain, out target)
                : TryResolveRewardTarget(hamster, chain, out target);
        }

        /// <summary>
        /// Определяет current-lane угрозу, от которой нужно уйти на другую roof-line.
        /// </summary>
        private static bool TryResolveDefensiveTarget(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out RoofSwitchLaneTarget target)
        {
            // Инициализирует результат.
            target = default;

            // Проверяет входные данные.
            if (hamster == null || chain == null)
                return false;

            // Ищет threat context.
            if (!TryFindCurrentLaneThreatContext(
                    chain,
                    out ObstacleSnapshot contextObstacle,
                    out int contextObstacleIndex))
            {
                return false;
            }

            // Возвращает target на противоположной roof-line.
            target = new RoofSwitchLaneTarget(
                contextObstacle,
                contextObstacleIndex,
                targetBottomLine: !hamster.IsOnBottomLine,
                CollectibleObjectiveValue.None);
            return true;
        }

        /// <summary>
        /// Определяет полезный collectable на другой roof-line как target context.
        /// </summary>
        private static bool TryResolveRewardTarget(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out RoofSwitchLaneTarget target)
        {
            // Инициализирует результат.
            target = default;

            // Проверяет входные данные.
            if (hamster == null || chain == null)
                return false;

            // Ищет collectable context.
            if (!TryFindPositiveCollectibleContext(
                    hamster,
                    chain,
                    out ObstacleSnapshot contextObstacle,
                    out int contextObstacleIndex,
                    out CollectibleObjectiveValue objectiveValue))
            {
                return false;
            }

            // Возвращает target на roof-line найденного collectable.
            target = new RoofSwitchLaneTarget(
                contextObstacle,
                contextObstacleIndex,
                chain.First.IsBottomLine,
                objectiveValue);
            return true;
        }

        /// <summary>
        /// Ищет первую current-lane угрозу для defensive roof switch-lane.
        /// </summary>
        private static bool TryFindCurrentLaneThreatContext(
            ObstacleChain chain,
            out ObstacleSnapshot contextObstacle,
            out int contextObstacleIndex)
        {
            // Инициализирует результат.
            contextObstacle = null;
            contextObstacleIndex = -1;
            if (chain == null)
                return false;

            // Ищет threat role в chain.
            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!element.HasRole(ObstacleRole.BlockingThreat)
                    && !element.HasRole(ObstacleRole.RoofOccupantHazard))
                {
                    continue;
                }

                contextObstacle = element.Obstacle;
                contextObstacleIndex = element.WorldIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ищет первый полезный collectable для reward roof switch-lane.
        /// </summary>
        private static bool TryFindPositiveCollectibleContext(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out ObstacleSnapshot collectible,
            out int collectibleIndex,
            out CollectibleObjectiveValue objectiveValue)
        {
            // Инициализирует результат.
            collectible = null;
            collectibleIndex = -1;
            objectiveValue = CollectibleObjectiveValue.None;
            if (hamster == null || chain == null)
                return false;

            // Ищет полезный collectable.
            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!element.HasRole(ObstacleRole.Collectible))
                    continue;

                if (!CollectibleValuePolicy.TryGetPositiveValue(
                        hamster,
                        element.Obstacle,
                        out objectiveValue))
                {
                    continue;
                }

                collectible = element.Obstacle;
                collectibleIndex = element.WorldIndex;
                return true;
            }

            return false;
        }
    }
}
