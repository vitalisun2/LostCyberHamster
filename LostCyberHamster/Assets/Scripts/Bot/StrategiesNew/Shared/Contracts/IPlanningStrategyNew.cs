using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.Contracts
{
    /// <summary>
    /// Описывает role-based planning-стратегию для нового path генерации действий.
    /// </summary>
    internal interface IPlanningStrategyNew
    {
        /// <summary>
        /// Возвращает тип действия, который обслуживает стратегия.
        /// </summary>
        BotActionKind ActionKind { get; }

        IActionExecutionHandler Executor { get; }

        ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет релевантные кандидаты действий для role-based точки решения.
        /// </summary>
        void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPointNew decisionPoint,
            List<PlannedAction> actions);
    }
}
