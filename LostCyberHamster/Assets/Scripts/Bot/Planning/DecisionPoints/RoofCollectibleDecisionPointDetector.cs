using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит roof-specific collectable decision points, которые обычный nearest-chain detector не должен угадывать.
    /// </summary>
    internal sealed class RoofCollectibleDecisionPointDetector
    {
        private const float RoofCollectibleVerticalEpsilon = 0.05f;

        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();

        /// <summary>
        /// Пытается построить current-lane collectable decision point на passive roof path.
        /// </summary>
        public bool TryDetectCurrentPassiveRoofCollectibles(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;
            if (!CanDetectPassiveRoofCollectibles(planningState, worldSnapshot))
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            var elements = new List<ObstacleChainElement>();
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanUseAsPassiveRoofCollectible(
                        planningState,
                        worldSnapshot,
                        hamster,
                        obstacle))
                {
                    continue;
                }

                elements.Add(new ObstacleChainElement(
                    obstacle,
                    obstacleIndex,
                    ObstacleRoleClassifier.GetRoles(
                        planningState,
                        worldSnapshot,
                        obstacle)));
            }

            if (elements.Count == 0)
                return false;

            elements.Sort((left, right) => left.Obstacle.LeftX.CompareTo(right.Obstacle.LeftX));
            decisionPoint = new DecisionPoint(new ObstacleChain(elements));
            return true;
        }

        /// <summary>
        /// Пытается построить opposite-lane roof reward route, пропуская ближние roof-chain без collectable.
        /// </summary>
        public bool TryDetectOppositeRoofCollectibleRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;
            if (!CanDetectPassiveRoofCollectibles(planningState, worldSnapshot))
                return false;

            bool oppositeBottomLine = !planningState.Hamster.IsOnBottomLine;
            if (!TryFindNearestOppositeRoofCollectibleSupport(
                    planningState,
                    worldSnapshot,
                    oppositeBottomLine,
                    out ObstacleSnapshot collectible,
                    out int supportIndex))
            {
                return false;
            }

            if (!_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    supportIndex,
                    oppositeBottomLine,
                    out ObstacleChain chain))
            {
                return false;
            }

            if (!chain.ContainsObstacle(collectible)
                || !CollectibleValuePolicy.HasPositiveCollectible(planningState.Hamster, chain))
            {
                return false;
            }

            decisionPoint = new DecisionPoint(chain);
            return true;
        }

        private static bool CanDetectPassiveRoofCollectibles(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            return hamster != null
                && worldSnapshot?.Obstacles != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && hamster.RoofSupportInstanceId.HasValue
                && !hamster.IsShifting;
        }

        private static bool CanUseAsPassiveRoofCollectible(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle)
        {
            if (obstacle == null
                || obstacle.IsRemovedInPlanning
                || obstacle.RightX <= hamster.HamsterLeftX
                || obstacle.IsBottomLine != hamster.IsOnBottomLine
                || !ObstacleClassifier.IsCollectible(obstacle.ObstacleType))
            {
                return false;
            }

            return RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                planningState,
                worldSnapshot,
                obstacle,
                out _,
                out _);
        }

        private static bool TryFindNearestOppositeRoofCollectibleSupport(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool supportBottomLine,
            out ObstacleSnapshot collectible,
            out int supportIndex)
        {
            collectible = null;
            supportIndex = -1;
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            float bestDistance = float.MaxValue;
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanUseAsOppositeRoofRewardCollectible(
                        hamster,
                        obstacle,
                        supportBottomLine))
                {
                    continue;
                }

                if (!CollectibleValuePolicy.TryGetPositiveValue(
                        hamster,
                        obstacle,
                        out _))
                {
                    continue;
                }

                if (!TryFindRoofSupportForCollectible(
                        worldSnapshot,
                        supportBottomLine,
                        obstacle,
                        out int candidateSupportIndex))
                {
                    continue;
                }

                float candidateDistance = GetForwardDistance(hamster, obstacle);
                if (candidateDistance >= bestDistance)
                    continue;

                collectible = obstacle;
                supportIndex = candidateSupportIndex;
                bestDistance = candidateDistance;
            }

            return collectible != null;
        }

        private static bool CanUseAsOppositeRoofRewardCollectible(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            bool supportBottomLine)
        {
            return obstacle != null
                && !obstacle.IsRemovedInPlanning
                && obstacle.RightX > hamster.HamsterLeftX
                && obstacle.IsBottomLine == supportBottomLine
                && ObstacleClassifier.IsCollectible(obstacle.ObstacleType);
        }

        private static bool TryFindRoofSupportForCollectible(
            WorldSnapshot worldSnapshot,
            bool supportBottomLine,
            ObstacleSnapshot collectible,
            out int supportIndex)
        {
            supportIndex = -1;
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot support = worldSnapshot.Obstacles[obstacleIndex];
                if (support == null
                    || support.IsRemovedInPlanning
                    || support.IsBottomLine != supportBottomLine
                    || !ObstacleClassifier.IsObstacleWithRoof(support.ObstacleType))
                {
                    continue;
                }

                if (!OverlapsX(collectible, support))
                    continue;

                if (!IsAboveRoofSupport(collectible, support))
                    continue;

                supportIndex = obstacleIndex;
                return true;
            }

            return false;
        }

        private static float GetForwardDistance(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle)
        {
            return Math.Max(0f, obstacle.LeftX - hamster.HamsterRightX);
        }

        private static bool OverlapsX(
            ObstacleSnapshot left,
            ObstacleSnapshot right)
        {
            return left.LeftX < right.RightX
                && left.RightX > right.LeftX;
        }

        private static bool IsAboveRoofSupport(
            ObstacleSnapshot collectible,
            ObstacleSnapshot support)
        {
            return collectible.BottomY >= support.TopY - RoofCollectibleVerticalEpsilon;
        }
    }
}
