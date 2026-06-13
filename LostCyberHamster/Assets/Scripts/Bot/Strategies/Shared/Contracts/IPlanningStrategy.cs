using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.Contracts
{
    /// <summary>
    /// Результат попытки применить planning-стратегию к role-based точке решения.
    /// </summary>
    internal sealed class PlanningStrategyResult
    {
        private PlanningStrategyResult(
            bool isApplicable,
            IReadOnlyList<PlannedAction> actions,
            StrategyDeadEndReason deadEndReason)
        {
            IsApplicable = isApplicable;
            Actions = actions ?? Array.Empty<PlannedAction>();
            DeadEndReason = deadEndReason;
        }

        public bool IsApplicable { get; }
        public IReadOnlyList<PlannedAction> Actions { get; }
        public StrategyDeadEndReason DeadEndReason { get; }
        public bool HasActions => Actions.Count > 0;
        public bool HasDeadEndReason => DeadEndReason != null;

        public static PlanningStrategyResult NotApplicable()
        {
            return new PlanningStrategyResult(
                isApplicable: false,
                actions: Array.Empty<PlannedAction>(),
                deadEndReason: null);
        }

        public static PlanningStrategyResult NoAction()
        {
            return new PlanningStrategyResult(
                isApplicable: true,
                actions: Array.Empty<PlannedAction>(),
                deadEndReason: null);
        }

        public static PlanningStrategyResult FromActions(IReadOnlyList<PlannedAction> actions)
        {
            return new PlanningStrategyResult(
                isApplicable: true,
                actions: actions,
                deadEndReason: null);
        }

        public static PlanningStrategyResult FromAction(PlannedAction action)
        {
            return action == null
                ? NoAction()
                : FromActions(new[] { action });
        }

        public static PlanningStrategyResult DeadEnd(string strategyName, string message)
        {
            return new PlanningStrategyResult(
                isApplicable: true,
                actions: Array.Empty<PlannedAction>(),
                deadEndReason: new StrategyDeadEndReason(strategyName, message));
        }
    }

    /// <summary>
    /// Короткая причина, по которой применимая стратегия не смогла создать action.
    /// </summary>
    internal sealed class StrategyDeadEndReason
    {
        public StrategyDeadEndReason(string strategyName, string message)
        {
            StrategyName = string.IsNullOrWhiteSpace(strategyName) ? "UnknownStrategy" : strategyName;
            Message = string.IsNullOrWhiteSpace(message)
                ? "Применимая стратегия не нашла безопасного действия."
                : message;
        }

        public string StrategyName { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"{StrategyName}: {Message}";
        }
    }

    /// <summary>
    /// Описывает role-based planning-стратегию для нового path генерации действий.
    /// </summary>
    internal interface IPlanningStrategy
    {
        /// <summary>
        /// Возвращает тип действия, который обслуживает стратегия.
        /// </summary>
        BotActionKind ActionKind { get; }

        IActionExecutionHandler Executor { get; }

        ISimulator Simulator { get; }

        /// <summary>
        /// Возвращает кандидаты действий или dead-end причину для применимой role-based точки решения.
        /// </summary>
        PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint);
    }
}
