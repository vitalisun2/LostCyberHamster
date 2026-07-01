using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит collectable decision points для текущего пути и reward routes на противоположную линию.
    /// </summary>
    internal sealed class CollectibleDecisionPointDetector
    {
        /// <summary>
        /// Допуск vertical-проверки, что collectable расположен на roof support.
        /// </summary>
        private const float RoofCollectibleVerticalEpsilon = 0.05f;

        /// <summary>
        /// Строит obstacle chain для line collectables и opposite roof support.
        /// </summary>
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();

        /// <summary>
        /// Пытается построить collectable decision point на текущем достижимом пути.
        /// </summary>
        public bool TryDetectCurrentCollectibles(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Проверяет входные данные.
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Проверяет roof collectables на текущем passive path.
            if (CanDetectRoofCollectibles(planningState, worldSnapshot))
            {
                return TryDetectCurrentRoofCollectibles(
                    planningState,
                    worldSnapshot,
                    out decisionPoint);
            }

            // Проверяет ordinary current-line collectables.
            return TryDetectGroundCollectibles(
                planningState,
                worldSnapshot,
                planningState.IsOnBottomLine,
                requirePositiveValue: false,
                out decisionPoint);
        }

        /// <summary>
        /// Пытается построить collectable route decision point на противоположной линии.
        /// </summary>
        public bool TryDetectOppositeCollectibleRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Проверяет входные данные.
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Проверяет reward route из roof state.
            if (CanDetectRoofCollectibles(planningState, worldSnapshot))
            {
                if (TryDetectOppositeRoofCollectibleRoute(
                        planningState,
                        worldSnapshot,
                        out decisionPoint))
                {
                    return true;
                }

                return TryDetectOppositeRoadCollectibleRoute(
                    planningState,
                    worldSnapshot,
                    out decisionPoint);
            }

            // Проверяет ordinary opposite-line reward route.
            return TryDetectGroundCollectibles(
                planningState,
                worldSnapshot,
                !planningState.IsOnBottomLine,
                requirePositiveValue: true,
                out decisionPoint);
        }

        /// <summary>
        /// Пытается построить optional-only collectable chain на выбранной ground lane.
        /// </summary>
        private bool TryDetectGroundCollectibles(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            bool requirePositiveValue,
            out DecisionPoint decisionPoint)
        {
            // Проверяет входные данные.
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Строит ближайшую line-chain.
            int firstDetectionIndex = GetGroundCollectibleStartIndex(
                planningState,
                focusBottomLine);
            if (!_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    firstDetectionIndex,
                    focusBottomLine,
                    out ObstacleChain chain))
            {
                return false;
            }

            // Оставляет только optional collectable chains.
            if (chain.HasAnyRequiredPlanningRole())
                return false;

            if (requirePositiveValue
                && !CollectibleValuePolicy.HasPositiveCollectible(
                    planningState.Hamster,
                    chain))
            {
                return false;
            }

            // Возвращает collectable decision point.
            decisionPoint = new DecisionPoint(chain);
            return true;
        }

        /// <summary>
        /// Возвращает старт ordinary collectable detection на выбранной ground lane.
        /// </summary>
        private static int GetGroundCollectibleStartIndex(
            PlanningState planningState,
            bool focusBottomLine)
        {
            // Противоположная линия анализируется с начала snapshot-а.
            if (focusBottomLine != planningState.IsOnBottomLine)
                return 0;

            // Текущая линия продолжает scan от planning cursor.
            return planningState.NextObstacleIndex;
        }

        /// <summary>
        /// Пытается построить current-lane collectable decision point на passive roof path.
        /// </summary>
        private static bool TryDetectCurrentRoofCollectibles(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Собирает collectables на текущем passive roof path.
            decisionPoint = null;
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

            // Проверяет наличие кандидатов.
            if (elements.Count == 0)
                return false;

            // Возвращает ordered decision point.
            elements.Sort((left, right) => left.Obstacle.LeftX.CompareTo(right.Obstacle.LeftX));
            decisionPoint = new DecisionPoint(new ObstacleChain(elements));
            return true;
        }

        /// <summary>
        /// Пытается построить opposite-lane roof reward route, пропуская ближние roof-chain без collectable.
        /// </summary>
        private bool TryDetectOppositeRoofCollectibleRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Ищет ближайшую roof support с полезным collectable.
            decisionPoint = null;
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

            // Строит chain от найденной support.
            if (!_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    supportIndex,
                    oppositeBottomLine,
                    out ObstacleChain chain))
            {
                return false;
            }

            // Проверяет, что chain действительно содержит полезный collectable.
            if (!chain.ContainsObstacle(collectible)
                || !CollectibleValuePolicy.HasPositiveCollectible(planningState.Hamster, chain))
            {
                return false;
            }

            // Возвращает reward decision point.
            decisionPoint = new DecisionPoint(chain);
            return true;
        }

        /// <summary>
        /// Пытается построить opposite-lane road reward route из roof state.
        /// </summary>
        private bool TryDetectOppositeRoadCollectibleRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Ищет ближайший road collectable на противоположной линии.
            decisionPoint = null;
            bool oppositeBottomLine = !planningState.Hamster.IsOnBottomLine;
            if (!TryFindNearestOppositeRoadCollectible(
                    planningState,
                    worldSnapshot,
                    oppositeBottomLine,
                    out ObstacleSnapshot collectible,
                    out int collectibleIndex))
            {
                return false;
            }

            // Строит optional chain от найденного collectable.
            if (!_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    collectibleIndex,
                    oppositeBottomLine,
                    out ObstacleChain chain))
            {
                return false;
            }

            // Проверяет, что route ведет именно к полезному road collectable.
            if (!chain.ContainsObstacle(collectible)
                || chain.HasAnyRequiredPlanningRole()
                || !CollectibleValuePolicy.HasPositiveCollectible(planningState.Hamster, chain))
            {
                return false;
            }

            // Возвращает reward decision point.
            decisionPoint = new DecisionPoint(chain);
            return true;
        }

        /// <summary>
        /// Проверяет, можно ли искать roof collectables из текущего planning-состояния.
        /// </summary>
        private static bool CanDetectRoofCollectibles(
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

        /// <summary>
        /// Проверяет, может ли collectable быть подобран пассивно на текущей roof-line.
        /// </summary>
        private static bool CanUseAsPassiveRoofCollectible(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle)
        {
            // Проверяет базовые признаки collectable.
            if (obstacle == null
                || obstacle.IsRemovedInPlanning
                || obstacle.RightX <= hamster.HamsterLeftX
                || obstacle.IsBottomLine != hamster.IsOnBottomLine
                || !ObstacleClassifier.IsCollectible(obstacle.ObstacleType))
            {
                return false;
            }

            // Проверяет наличие roof support под collectable.
            return RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                planningState,
                worldSnapshot,
                obstacle,
                out _,
                out _);
        }

        /// <summary>
        /// Ищет ближайший полезный collectable на opposite roof-line и его support.
        /// </summary>
        private static bool TryFindNearestOppositeRoofCollectibleSupport(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool supportBottomLine,
            out ObstacleSnapshot collectible,
            out int supportIndex)
        {
            // Инициализирует результат.
            collectible = null;
            supportIndex = -1;

            // Проверяет входные данные.
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Перебирает collectable-кандидатов.
            float bestDistance = float.MaxValue;
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanUseAsOppositeRewardCollectible(
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

                // Запоминает ближайший валидный кандидат.
                collectible = obstacle;
                supportIndex = candidateSupportIndex;
                bestDistance = candidateDistance;
            }

            // Возвращает наличие найденного кандидата.
            return collectible != null;
        }

        /// <summary>
        /// Ищет ближайший полезный road collectable на opposite lane.
        /// </summary>
        private static bool TryFindNearestOppositeRoadCollectible(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool targetBottomLine,
            out ObstacleSnapshot collectible,
            out int collectibleIndex)
        {
            // Инициализирует результат.
            collectible = null;
            collectibleIndex = -1;

            // Проверяет входные данные.
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Перебирает collectable-кандидатов.
            float bestDistance = float.MaxValue;
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanUseAsOppositeRewardCollectible(
                        hamster,
                        obstacle,
                        targetBottomLine))
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

                if (TryFindRoofSupportForCollectible(
                        worldSnapshot,
                        targetBottomLine,
                        obstacle,
                        out _))
                {
                    continue;
                }

                float candidateDistance = GetForwardDistance(hamster, obstacle);
                if (candidateDistance >= bestDistance)
                    continue;

                // Запоминает ближайший road collectable.
                collectible = obstacle;
                collectibleIndex = obstacleIndex;
                bestDistance = candidateDistance;
            }

            // Возвращает наличие найденного кандидата.
            return collectible != null;
        }

        /// <summary>
        /// Проверяет базовую пригодность collectable на opposite lane.
        /// </summary>
        private static bool CanUseAsOppositeRewardCollectible(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            bool targetBottomLine)
        {
            return obstacle != null
                && !obstacle.IsRemovedInPlanning
                && obstacle.RightX > hamster.HamsterLeftX
                && obstacle.IsBottomLine == targetBottomLine
                && ObstacleClassifier.IsCollectible(obstacle.ObstacleType);
        }

        /// <summary>
        /// Ищет roof support, на которой расположен collectable.
        /// </summary>
        private static bool TryFindRoofSupportForCollectible(
            WorldSnapshot worldSnapshot,
            bool supportBottomLine,
            ObstacleSnapshot collectible,
            out int supportIndex)
        {
            // Инициализирует результат.
            supportIndex = -1;

            // Перебирает roof support-кандидатов.
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

                // Возвращает найденную support.
                supportIndex = obstacleIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает forward-дистанцию от хомяка до obstacle.
        /// </summary>
        private static float GetForwardDistance(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle)
        {
            return Math.Max(0f, obstacle.LeftX - hamster.HamsterRightX);
        }

        /// <summary>
        /// Проверяет X-пересечение двух obstacle.
        /// </summary>
        private static bool OverlapsX(
            ObstacleSnapshot left,
            ObstacleSnapshot right)
        {
            return left.LeftX < right.RightX
                && left.RightX > right.LeftX;
        }

        /// <summary>
        /// Проверяет, что collectable расположен над roof support.
        /// </summary>
        private static bool IsAboveRoofSupport(
            ObstacleSnapshot collectible,
            ObstacleSnapshot support)
        {
            return collectible.BottomY >= support.TopY - RoofCollectibleVerticalEpsilon;
        }
    }
}
