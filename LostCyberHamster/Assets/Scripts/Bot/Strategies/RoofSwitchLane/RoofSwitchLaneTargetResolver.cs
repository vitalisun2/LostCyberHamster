using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Определяет target context для смены линии с крыши на другую линию.
    /// </summary>
    internal sealed class RoofSwitchLaneTargetResolver
    {
        /// <summary>
        /// Определяет target contexts для defensive или reward roof switch-lane сценария.
        /// </summary>
        public bool TryResolveTargets(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out IReadOnlyList<RoofSwitchLaneTarget> targets)
        {
            // Инициализирует результат.
            targets = null;

            // Проверяет входные данные.
            HamsterSnapshot hamster = planningState?.Hamster;
            ObstacleChain chain = decisionPoint?.Chain;
            if (hamster == null || chain == null)
                return false;

            // Выбирает сценарий по линии decision chain.
            bool chainIsCurrentLane = chain.First.IsBottomLine == hamster.IsOnBottomLine;
            if (chainIsCurrentLane)
                return TryResolveDefensiveTarget(hamster, chain, out targets);

            return TryResolveRewardTargets(hamster, chain, out targets);
        }

        /// <summary>
        /// Определяет current-lane угрозу, от которой нужно уйти на другую roof-line.
        /// </summary>
        private static bool TryResolveDefensiveTarget(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out IReadOnlyList<RoofSwitchLaneTarget> targets)
        {
            // Инициализирует результат.
            targets = null;

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

            // Возвращает target на противоположной линии.
            targets = new[]
            {
                new RoofSwitchLaneTarget(
                    contextObstacle,
                    contextObstacleIndex,
                    targetBottomLine: !hamster.IsOnBottomLine,
                    CollectibleObjectiveValue.None)
            };
            return true;
        }

        /// <summary>
        /// Определяет полезные collectables на другой линии как target contexts.
        /// </summary>
        private static bool TryResolveRewardTargets(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out IReadOnlyList<RoofSwitchLaneTarget> targets)
        {
            // Инициализирует результат.
            targets = null;

            // Проверяет входные данные.
            if (hamster == null || chain == null)
                return false;

            // Собирает collectable contexts.
            var resolvedTargets = new List<RoofSwitchLaneTarget>();
            CollectPositiveCollectibleTargets(
                hamster,
                chain,
                resolvedTargets);

            if (resolvedTargets.Count == 0)
                return false;

            // Возвращает target-ы на линии collectables.
            targets = resolvedTargets;
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
        /// Добавляет полезные collectables для reward roof switch-lane.
        /// </summary>
        private static void CollectPositiveCollectibleTargets(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            List<RoofSwitchLaneTarget> targets)
        {
            // Проверяет входные данные.
            if (hamster == null || chain == null || targets == null)
                return;

            // Ищет полезные collectables.
            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!element.HasRole(ObstacleRole.Collectible))
                    continue;

                if (!CollectibleValuePolicy.TryGetPositiveValue(
                        hamster,
                        element.Obstacle,
                        out CollectibleObjectiveValue objectiveValue))
                {
                    continue;
                }

                targets.Add(new RoofSwitchLaneTarget(
                    element.Obstacle,
                    element.WorldIndex,
                    element.Obstacle.IsBottomLine,
                    objectiveValue));
            }
        }
    }
}
