using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Находит следующую обязательную для реакции ситуацию в projected world snapshot.
    /// </summary>
    public sealed class DecisionPointDetector
    {
        /// <summary>
        /// Находит roof occupants на текущей passive roof-chain до общего пропуска крыш.
        /// </summary>
        private readonly RoofOccupantHazardDetector _roofOccupantHazardDetector = new();

        /// <summary>
        /// Ограничивает количество obstacles в одной chain-ситуации.
        /// </summary>
        private const int _maxChainLength = 3;

        /// <summary>
        /// Пытается найти ближайшую blocking chain-ситуацию или high-priority jump-on opportunity.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;

            // Отсекает неполный вход.
            if (planningState == null || worldSnapshot == null)
                return false;

            // Сначала обрабатывает hazards, стоящие на текущей passive roof-chain.
            if (_roofOccupantHazardDetector.TryDetect(
                    planningState,
                    worldSnapshot,
                    out int roofOccupantHazardIndex))
            {
                decisionPoint = new DecisionPoint(BuildChain(
                    planningState,
                    worldSnapshot,
                    roofOccupantHazardIndex,
                    planningState.IsOnBottomLine));
                return true;
            }

            // Выбирает стартовый obstacle.
            int firstObstacleIndex = GetFirstDetectionIndex(planningState, worldSnapshot);

            bool hasBlockingThreat = TryFindBlockingThreat(
                planningState,
                worldSnapshot,
                firstObstacleIndex,
                out int blockingThreatIndex);

            if (hasBlockingThreat)
            {
                decisionPoint = new DecisionPoint(BuildChain(
                    planningState,
                    worldSnapshot,
                    blockingThreatIndex,
                    planningState.IsOnBottomLine));
                return true;
            }

            return TryDetectJumpOnOpportunity(planningState, worldSnapshot, out decisionPoint);
        }

        /// <summary>
        /// Пытается найти видимую target-oriented jump-on opportunity независимо от ближайшей угрозы.
        /// </summary>
        public bool TryDetectJumpOnOpportunity(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;
            if (planningState == null || worldSnapshot == null)
                return false;

            int firstObstacleIndex = GetFirstDetectionIndex(planningState, worldSnapshot);
            if (!TryFindJumpOnOpportunity(
                    planningState,
                    worldSnapshot,
                    firstObstacleIndex,
                    out int opportunityIndex,
                    out bool opportunityBottomLine))
            {
                return false;
            }

            ObstacleChain opportunityChain = BuildChain(
                planningState,
                worldSnapshot,
                opportunityIndex,
                opportunityBottomLine);
            decisionPoint = new DecisionPoint(
                opportunityChain,
                DecisionPointKind.JumpOnOpportunity,
                TryFindBlockingThreat(planningState, worldSnapshot, firstObstacleIndex, out int blockingThreatIndex)
                    ? worldSnapshot.Obstacles[blockingThreatIndex]
                    : null);
            return true;
        }

        /// <summary>
        /// Возвращает index obstacle, с которого detector должен начать поиск decision point.
        /// </summary>
        private static int GetFirstDetectionIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            // Готовит default start.
            int defaultDetectionIndex = planningState.NextObstacleIndex;
            HamsterSnapshot hamster = planningState.Hamster;

            // Разделяет ground и roof-сценарии.
            if (hamster == null || !hamster.IsOnRoof)
                return defaultDetectionIndex;

            // Пробует пропустить passive roof chain.
            if (RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out int lastRoofIndex))
            {
                int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
                if (firstIndexAfterPassiveRoofs > defaultDetectionIndex)
                {
                    DebugManager.DiagLogVerbose(
                        $"[Bot PLAN] SKIP_PASSIVE_ROOF_CHAIN lastRoof={lastRoof.ObstacleType} " +
                        $"index={lastRoofIndex} instanceId={lastRoof.InstanceId} " +
                        $"leftX={lastRoof.LeftX:F2} rightX={lastRoof.RightX:F2}");

                    return firstIndexAfterPassiveRoofs;
                }
            }

            // Возвращает default fallback.
            return defaultDetectionIndex;
        }

        /// <summary>
        /// Строит obstacle chain, начиная с первого obstacle decision point.
        /// </summary>
        private static ObstacleChain BuildChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            bool chainBottomLine)
        {
            // Инициализирует chain первым obstacle.
            var obstacles = new List<ObstacleSnapshot>();
            var indices = new List<int>();
            ObstacleSnapshot firstObstacle = worldSnapshot.Obstacles[firstObstacleIndex];
            obstacles.Add(firstObstacle);
            indices.Add(firstObstacleIndex);

            // Расширяет chain близкими damaging obstacles.
            float previousRightX = firstObstacle.RightX;
            for (int obstacleIndex = firstObstacleIndex + 1;
                 obstacleIndex < worldSnapshot.Obstacles.Count && obstacles.Count < _maxChainLength;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != chainBottomLine)
                    continue;

                if (RoofRunProjection.IsPassiveRoofContinuation(planningState, worldSnapshot, obstacle))
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                float gap = obstacle.LeftX - previousRightX;
                if (gap >= planningState.Hamster.Width)
                    break;

                obstacles.Add(obstacle);
                indices.Add(obstacleIndex);

                if (obstacle.RightX > previousRightX)
                    previousRightX = obstacle.RightX;
            }

            // Возвращает готовую chain.
            return new ObstacleChain(obstacles, indices);
        }

        /// <summary>
        /// Находит ближайшую обязательную угрозу на текущей линии хомяка.
        /// </summary>
        private static bool TryFindBlockingThreat(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            out int blockingThreatIndex)
        {
            blockingThreatIndex = -1;

            for (int obstacleIndex = firstObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!IsBlockingThreat(planningState, worldSnapshot, obstacle, obstacleIndex))
                    continue;

                blockingThreatIndex = obstacleIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, является ли obstacle ближайшей угрозой текущей линии.
        /// </summary>
        private static bool IsBlockingThreat(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            int obstacleIndex)
        {
            if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                return false;

            if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                return false;

            if (RoofRunProjection.IsPassiveRoofContinuation(planningState, worldSnapshot, obstacle))
            {
                DebugManager.DiagLogVerbose(
                    $"[Bot PLAN] SKIP_ROOF_CONTINUATION obstacle={obstacle.ObstacleType} " +
                    $"index={obstacleIndex} instanceId={obstacle.InstanceId} " +
                    $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2}");
                return false;
            }

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }

        /// <summary>
        /// Находит off-line opportunity-chain, где после смены линии можно выполнить jump-on target.
        /// </summary>
        private static bool TryFindJumpOnOpportunity(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            out int opportunityIndex,
            out bool opportunityBottomLine)
        {
            opportunityIndex = -1;
            opportunityBottomLine = false;

            if (!CanSearchJumpOnOpportunity(planningState))
                return false;

            for (int obstacleIndex = firstObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine == planningState.IsOnBottomLine)
                    continue;

                if (obstacle.LeftX > worldSnapshot.ScreenRightEdgeX)
                    return false;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                ObstacleChain chain = BuildChain(
                    planningState,
                    worldSnapshot,
                    obstacleIndex,
                    obstacle.IsBottomLine);
                if (!chain.TryFindFirstGroundJumpOnTarget(
                        obstacle.IsBottomLine,
                        out ObstacleSnapshot targetObstacle,
                        out _,
                        out _))
                {
                    continue;
                }

                if (targetObstacle.LeftX > worldSnapshot.ScreenRightEdgeX)
                    continue;

                opportunityIndex = obstacleIndex;
                opportunityBottomLine = obstacle.IsBottomLine;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, можно ли искать target-oriented jump-on opportunity.
        /// </summary>
        private static bool CanSearchJumpOnOpportunity(PlanningState planningState)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            return hamster != null
                && !hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.Energy >= PlanningInterestRules.HighPriorityJumpOnEnergyThreshold;
        }

    }
}
