using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Генерирует role-based candidate-действия бота через новую точку решения и новые planning-стратегии.
    /// </summary>
    public sealed class ActionGeneratorNew
    {
        private readonly IReadOnlyList<IPlanningStrategyNew> _strategies;
        private readonly DecisionPointDetectorNew _decisionPointDetector = new DecisionPointDetectorNew();

        /// <summary>
        /// Создает role-based generator поверх списка активных новых стратегий.
        /// </summary>
        internal ActionGeneratorNew(IReadOnlyList<IPlanningStrategyNew> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategyNew>();
        }

        /// <summary>
        /// Генерирует список действий, доступных из текущего planning-состояния и snapshot мира.
        /// </summary>
        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            // Проверяет входные данные.
            var plannedActions = new List<PlannedAction>();
            if (planningState == null || worldSnapshot == null)
                return plannedActions;

            // Проецирует мир в состояние планирования.
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return plannedActions;

            // Строит единственную role-based planning-ситуацию.
            if (!_decisionPointDetector.TryDetect(
                    planningState,
                    projectedWorldSnapshot,
                    out DecisionPointNew decisionPoint))
            {
                LogNoDecisionPoint(planningState);
                return plannedActions;
            }

            // Собирает действия одним проходом по активным role-based стратегиям.
            CollectActionsForDecisionPoint(
                planningState,
                projectedWorldSnapshot,
                decisionPoint,
                plannedActions);

            // Логирует отсутствие подходящих действий для найденной planning-ситуации.
            if (plannedActions.Count == 0)
                LogNoActions(planningState, decisionPoint);

            return plannedActions;
        }

        /// <summary>
        /// Запрашивает у всех role-based planning-стратегий действия для указанной точки решения.
        /// </summary>
        private void CollectActionsForDecisionPoint(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPointNew decisionPoint,
            List<PlannedAction> plannedActions)
        {
            // Обходит новые стратегии в порядке их приоритета.
            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                _strategies[strategyIndex].CollectActions(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint,
                    plannedActions);
            }
        }

        /// <summary>
        /// Логирует отсутствие role-based точки решения.
        /// </summary>
        private static void LogNoDecisionPoint(PlanningState planningState)
        {
            // Проверяет входные данные.
            if (planningState?.Hamster == null)
                return;

            DebugManager.DiagLogVerbose(
                $"[Bot PLAN NEW] NO_DECISION " +
                $"nextObstacleIndex={planningState.NextObstacleIndex} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        /// <summary>
        /// Логирует найденную role-based точку решения, для которой стратегии не создали действий.
        /// </summary>
        private static void LogNoActions(PlanningState planningState, DecisionPointNew decisionPoint)
        {
            // Проверяет входные данные.
            if (planningState == null || decisionPoint?.Chain == null)
                return;

            ObstacleChainNew chain = decisionPoint.Chain;
            ObstacleChainElementNew firstElement = chain.First;
            ObstacleSnapshot firstObstacle = firstElement.Obstacle;

            DebugManager.DiagLogVerbose(
                $"[Bot PLAN NEW] NO_ACTIONS firstObstacle={firstObstacle.ObstacleType} " +
                $"roles={FormatRoles(firstElement.Roles)} " +
                $"chainCount={chain.Count} " +
                $"chainLeftX={chain.LeftX:F2} chainRightX={chain.RightX:F2} " +
                $"firstLeftX={firstObstacle.LeftX:F2} firstRightX={firstObstacle.RightX:F2} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        /// <summary>
        /// Форматирует роли obstacle для диагностического лога.
        /// </summary>
        private static string FormatRoles(IReadOnlyCollection<ObstacleRole> roles)
        {
            // Возвращает стабильное значение для пустого набора ролей.
            if (roles == null || roles.Count == 0)
                return "none";

            // Сортирует имена ролей, чтобы лог не зависел от порядка HashSet.
            var roleNames = new List<string>(roles.Count);
            foreach (ObstacleRole role in roles)
                roleNames.Add(role.ToString());

            roleNames.Sort(StringComparer.Ordinal);
            return string.Join("|", roleNames);
        }
    }
}
