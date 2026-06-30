using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Результат генерации actions для одного planning state.
    /// </summary>
    internal sealed class ActionGenerationResult
    {
        public ActionGenerationResult(
            IReadOnlyList<PlannedAction> actions,
            IReadOnlyList<StrategyDeadEndReason> deadEndReasons,
            bool hasUnresolvedPlanningSituation)
        {
            Actions = actions ?? Array.Empty<PlannedAction>();
            DeadEndReasons = deadEndReasons ?? Array.Empty<StrategyDeadEndReason>();
            HasUnresolvedPlanningSituation = hasUnresolvedPlanningSituation;
        }

        public IReadOnlyList<PlannedAction> Actions { get; }
        public IReadOnlyList<StrategyDeadEndReason> DeadEndReasons { get; }
        public bool HasUnresolvedPlanningSituation { get; }
        public bool HasDeadEndReasons => DeadEndReasons.Count > 0;

        public static ActionGenerationResult Empty()
        {
            return new ActionGenerationResult(
                Array.Empty<PlannedAction>(),
                Array.Empty<StrategyDeadEndReason>(),
                hasUnresolvedPlanningSituation: false);
        }
    }

    /// <summary>
    /// Генерирует role-based действия через decision points и planning strategies.
    /// </summary>
    public sealed class ActionGenerator
    {
        private readonly IReadOnlyList<IPlanningStrategy> _strategies;
        private readonly IPlanningStrategy _switchLaneStrategy;
        private readonly IPlanningStrategy _roofSwitchLaneStrategy;
        private readonly IPlanningStrategy _passiveCollectStrategy;
        private readonly IPlanningStrategy _passiveAdvanceStrategy;
        private readonly RouteDecisionPointDetector _routeDecisionPointDetector = new RouteDecisionPointDetector();
        private readonly CollectibleDecisionPointDetector _collectibleDecisionPointDetector =
            new CollectibleDecisionPointDetector();
        private readonly SuperFallbackActionDeduplicator _superFallbackDeduplicator =
            new SuperFallbackActionDeduplicator();

        /// <summary>
        /// Создает role-based generator поверх активных strategies.
        /// </summary>
        internal ActionGenerator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategy>();
            _switchLaneStrategy = FindStrategy(_strategies, BotActionKind.SwitchLane);
            _roofSwitchLaneStrategy = FindStrategy(_strategies, BotActionKind.RoofSwitchLane);
            _passiveCollectStrategy = FindStrategy(_strategies, BotActionKind.PassiveCollect);
            _passiveAdvanceStrategy = FindStrategy(_strategies, BotActionKind.PassiveAdvance);
        }

        /// <summary>
        /// Генерирует доступные действия из текущего planning-состояния и snapshot мира.
        /// </summary>
        internal ActionGenerationResult Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            var plannedActions = new List<PlannedAction>();
            var deadEndReasons = new List<StrategyDeadEndReason>();
            _superFallbackDeduplicator.Reset();
            if (planningState == null || worldSnapshot == null)
                return ActionGenerationResult.Empty();

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return ActionGenerationResult.Empty();

            bool hasCurrentDecisionPoint = _routeDecisionPointDetector.TryDetectCurrent(
                    planningState,
                    projectedWorldSnapshot,
                    out DecisionPoint currentDecisionPoint);

            bool hasOppositeDecisionPoint = _routeDecisionPointDetector.TryDetectOpposite(
                    planningState,
                    projectedWorldSnapshot,
                    out DecisionPoint oppositeDecisionPoint);

            if (hasCurrentDecisionPoint)
            {
                CollectActionsForDecisionPoint(
                    planningState,
                    projectedWorldSnapshot,
                    currentDecisionPoint,
                    plannedActions,
                    deadEndReasons);
            }

            CollectCurrentCollectibleActions(
                planningState,
                projectedWorldSnapshot,
                plannedActions,
                deadEndReasons);

            CollectOppositeCollectibleRouteActions(
                planningState,
                projectedWorldSnapshot,
                plannedActions,
                deadEndReasons);

            if (hasOppositeDecisionPoint)
            {
                CollectSwitchLaneEntryAction(
                    planningState,
                    projectedWorldSnapshot,
                    oppositeDecisionPoint,
                    plannedActions,
                    deadEndReasons);

                CollectPassiveAdvanceAction(
                    planningState,
                    projectedWorldSnapshot,
                    oppositeDecisionPoint,
                    plannedActions,
                    deadEndReasons);
            }

            if (!hasCurrentDecisionPoint
                && !hasOppositeDecisionPoint
                && plannedActions.Count == 0)
            {
                LogNoDecisionPoint(planningState);
                return new ActionGenerationResult(
                    plannedActions,
                    deadEndReasons,
                    hasUnresolvedPlanningSituation: false);
            }

            if (plannedActions.Count == 0 && hasCurrentDecisionPoint)
                LogNoActions(planningState, currentDecisionPoint);

            if (plannedActions.Count == 0 && deadEndReasons.Count > 0)
            {
                LogDeadEndContext(
                    planningState,
                    currentDecisionPoint,
                    oppositeDecisionPoint,
                    hasCurrentDecisionPoint,
                    hasOppositeDecisionPoint,
                    deadEndReasons);
            }

            return new ActionGenerationResult(
                plannedActions,
                deadEndReasons,
                hasUnresolvedPlanningSituation: hasCurrentDecisionPoint || deadEndReasons.Count > 0);
        }

        /// <summary>
        /// Добавляет collectable action для текущего достижимого пути.
        /// </summary>
        private void CollectCurrentCollectibleActions(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (_passiveCollectStrategy == null)
                return;

            if (!_collectibleDecisionPointDetector.TryDetectCurrentCollectibles(
                    planningState,
                    projectedWorldSnapshot,
                    out DecisionPoint currentCollectibleDecisionPoint))
            {
                return;
            }

            PlanningStrategyResult result = CollectFromStrategy(
                _passiveCollectStrategy,
                planningState,
                projectedWorldSnapshot,
                currentCollectibleDecisionPoint);

            ApplyStrategyResult(
                result,
                plannedActions,
                deadEndReasons);
        }

        /// <summary>
        /// Добавляет entry action к полезному collectable на противоположной линии.
        /// </summary>
        private void CollectOppositeCollectibleRouteActions(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (!_collectibleDecisionPointDetector.TryDetectOppositeCollectibleRoute(
                    planningState,
                    projectedWorldSnapshot,
                    out DecisionPoint oppositeCollectibleDecisionPoint))
            {
                return;
            }

            IPlanningStrategy entryStrategy = planningState.Hamster.IsOnRoof
                ? _roofSwitchLaneStrategy
                : _switchLaneStrategy;
            if (entryStrategy == null)
                return;

            PlanningStrategyResult result = CollectFromStrategy(
                entryStrategy,
                planningState,
                projectedWorldSnapshot,
                oppositeCollectibleDecisionPoint);

            ApplyStrategyResult(
                result,
                plannedActions,
                deadEndReasons);
        }

        /// <summary>
        /// Запрашивает действия у всех role-based strategies для decision point.
        /// </summary>
        private void CollectActionsForDecisionPoint(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                PlanningStrategyResult result = CollectFromStrategy(
                    _strategies[strategyIndex],
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint);

                ApplyStrategyResult(
                    result,
                    plannedActions,
                    deadEndReasons);
            }
        }

        /// <summary>
        /// Добавляет entry SwitchLane action для ветки другой линии.
        /// </summary>
        private void CollectSwitchLaneEntryAction(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint oppositeDecisionPoint,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (_switchLaneStrategy == null)
                return;

            PlanningStrategyResult result = CollectFromStrategy(
                _switchLaneStrategy,
                planningState,
                projectedWorldSnapshot,
                oppositeDecisionPoint);

            ApplyStrategyResult(
                result,
                plannedActions,
                deadEndReasons);
        }

        /// <summary>
        /// Добавляет no-input продвижение до момента, когда opposite-lane situation перестанет блокировать анализ.
        /// </summary>
        private void CollectPassiveAdvanceAction(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint oppositeDecisionPoint,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (_passiveAdvanceStrategy == null)
                return;

            PlanningStrategyResult result = CollectFromStrategy(
                _passiveAdvanceStrategy,
                planningState,
                projectedWorldSnapshot,
                oppositeDecisionPoint);

            ApplyStrategyResult(
                result,
                plannedActions,
                deadEndReasons);
        }

        /// <summary>
        /// Запрашивает actions у стратегии после дешевой проверки применимости.
        /// </summary>
        private static PlanningStrategyResult CollectFromStrategy(
            IPlanningStrategy strategy,
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint)
        {
            if (strategy == null)
                return PlanningStrategyResult.NotApplicable();

            if (!strategy.CanConsider(planningState, decisionPoint))
                return PlanningStrategyResult.NotApplicable();

            return strategy.CollectActions(
                planningState,
                projectedWorldSnapshot,
                decisionPoint);
        }

        /// <summary>
        /// Добавляет результат одной strategy в общий generation result.
        /// </summary>
        private void ApplyStrategyResult(
            PlanningStrategyResult result,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (result == null || !result.IsApplicable)
                return;

            for (int actionIndex = 0; actionIndex < result.Actions.Count; actionIndex++)
            {
                PlannedAction action = result.Actions[actionIndex];
                if (_superFallbackDeduplicator.TryAccept(action))
                    plannedActions.Add(action);
            }

            if (result.HasDeadEndReason)
                deadEndReasons.Add(result.DeadEndReason);
        }

        /// <summary>
        /// Находит strategy по action kind.
        /// </summary>
        private static IPlanningStrategy FindStrategy(
            IReadOnlyList<IPlanningStrategy> strategies,
            BotActionKind actionKind)
        {
            if (strategies == null)
                return null;

            for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy != null && strategy.ActionKind == actionKind)
                    return strategy;
            }

            return null;
        }

        /// <summary>
        /// Логирует planning context, который отличает тупиковую геометрию от ошибки выбора action.
        /// </summary>
        private static void LogDeadEndContext(
            PlanningState planningState,
            DecisionPoint currentDecisionPoint,
            DecisionPoint oppositeDecisionPoint,
            bool hasCurrentDecisionPoint,
            bool hasOppositeDecisionPoint,
            IReadOnlyList<StrategyDeadEndReason> deadEndReasons)
        {
            if (planningState?.Hamster == null || deadEndReasons == null)
                return;

            BotStrategyDiagnostics.LogDeadEndContext(
                planningState,
                currentDecisionPoint,
                oppositeDecisionPoint,
                hasCurrentDecisionPoint,
                hasOppositeDecisionPoint,
                deadEndReasons.Count);
        }

        /// <summary>
        /// Логирует отсутствие role-based decision point.
        /// </summary>
        private static void LogNoDecisionPoint(PlanningState planningState)
        {
            if (planningState?.Hamster == null)
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Verbose,
                $"[Bot PLAN NEW] NO_DECISION " +
                $"nextObstacleIndex={planningState.NextObstacleIndex} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        /// <summary>
        /// Логирует role-based decision point, для которого strategies не создали actions.
        /// </summary>
        private static void LogNoActions(PlanningState planningState, DecisionPoint decisionPoint)
        {
            if (planningState == null || decisionPoint?.Chain == null)
                return;

            ObstacleChain chain = decisionPoint.Chain;
            ObstacleChainElement firstElement = chain.First;
            ObstacleSnapshot firstObstacle = firstElement.Obstacle;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Verbose,
                $"[Bot PLAN NEW] NO_ACTIONS firstObstacle={firstObstacle.ObstacleType} " +
                $"roles={FormatRoles(firstElement.Roles)} " +
                $"chainCount={chain.Count} " +
                $"chainLeftX={chain.LeftX:F2} chainRightX={chain.RightX:F2} " +
                $"firstLeftX={firstObstacle.LeftX:F2} firstRightX={firstObstacle.RightX:F2} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        /// <summary>
        /// Форматирует obstacle roles для диагностического лога.
        /// </summary>
        private static string FormatRoles(IReadOnlyCollection<ObstacleRole> roles)
        {
            if (roles == null || roles.Count == 0)
                return "none";

            var roleNames = new List<string>(roles.Count);
            foreach (ObstacleRole role in roles)
                roleNames.Add(role.ToString());

            roleNames.Sort(StringComparer.Ordinal);
            return string.Join("|", roleNames);
        }
    }
}
