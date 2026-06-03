using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Выбирает focus lane и строит role-based decision point без сценарных builders.
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
            bool focusBottomLine = ResolveFocusBottomLine(
                planningState,
                worldSnapshot,
                firstDetectionIndex);

            if (!_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    focusBottomLine,
                    firstDetectionIndex,
                    out ObstacleChainNew chain))
            {
                return false;
            }

            decisionPoint = new DecisionPointNew(chain);
            return true;
        }

        /// <summary>
        /// Определяет focus lane: current lane или lane ближайшего huntable target.
        /// </summary>
        private static bool ResolveFocusBottomLine(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstDetectionIndex)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            bool currentBottomLine = planningState.IsOnBottomLine;
            if (!CanUseTargetFocus(hamster))
                return currentBottomLine;

            if (!TryFindNearestHuntableTarget(
                    hamster,
                    worldSnapshot,
                    currentBottomLine,
                    firstDetectionIndex,
                    out ObstacleSnapshot target))
            {
                return currentBottomLine;
            }

            return target.IsBottomLine;
        }

        /// <summary>
        /// Проверяет локальный predicate включения target-focus.
        /// </summary>
        private static bool CanUseTargetFocus(HamsterSnapshot hamster)
        {
            if (hamster == null)
                return false;

            bool canScanByState = hamster.HamsterState == HamsterStateEnum.Run
                || hamster.HamsterState == HamsterStateEnum.RoofRun;

            return canScanByState
                && !hamster.IsShifting
                && JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster);
        }

        /// <summary>
        /// Ищет ближайший target на обеих линиях до vision horizon.
        /// </summary>
        private static bool TryFindNearestHuntableTarget(
            HamsterSnapshot hamster,
            WorldSnapshot worldSnapshot,
            bool currentBottomLine,
            int firstObstacleIndex,
            out ObstacleSnapshot target)
        {
            target = null;
            float targetDistance = float.MaxValue;
            int startIndex = firstObstacleIndex < 0 ? 0 : firstObstacleIndex;

            for (int obstacleIndex = startIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle == null)
                    continue;

                if (obstacle.LeftX > worldSnapshot.VisionRightEdgeX)
                    break;

                if (obstacle.RightX <= hamster.HamsterLeftX)
                    continue;

                if (!IsHuntableTarget(hamster, obstacle))
                    continue;

                float distance = Math.Max(0f, obstacle.LeftX - hamster.HamsterRightX);
                if (!IsBetterTarget(obstacle, distance, target, targetDistance, currentBottomLine))
                    continue;

                target = obstacle;
                targetDistance = distance;
            }

            return target != null;
        }

        /// <summary>
        /// Проверяет, может ли текущий hamster state охотиться за obstacle как target.
        /// </summary>
        private static bool IsHuntableTarget(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle)
        {
            if (hamster.HamsterState == HamsterStateEnum.Run)
                return ObstacleClassifier.CanJumpOnGroundObstacle(obstacle.ObstacleType);

            if (hamster.HamsterState == HamsterStateEnum.RoofRun)
                return ObstacleClassifier.CanJumpOnFromRoofObstacle(obstacle.ObstacleType);

            return false;
        }

        /// <summary>
        /// Сравнивает кандидата с текущим target с tie-break в пользу current lane.
        /// </summary>
        private static bool IsBetterTarget(
            ObstacleSnapshot candidate,
            float candidateDistance,
            ObstacleSnapshot currentTarget,
            float currentDistance,
            bool currentBottomLine)
        {
            if (currentTarget == null)
                return true;

            const float DistanceEpsilon = 0.001f;
            if (candidateDistance < currentDistance - DistanceEpsilon)
                return true;

            if (candidateDistance > currentDistance + DistanceEpsilon)
                return false;

            return candidate.IsBottomLine == currentBottomLine
                && currentTarget.IsBottomLine != currentBottomLine;
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
