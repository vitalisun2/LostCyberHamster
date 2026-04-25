using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.Strategies.Shared.Interfaces
{
    /// <summary>
    /// Описывает planning-стратегию для одного семейства действий бота.
    /// </summary>
    internal interface IPlanningStrategy
    {
        /// <summary>
        /// Возвращает тип действия, который обслуживает стратегия.
        /// </summary>
        BotActionKind ActionKind { get; }

        IActionExecutionHandler Executor { get; }

        IRetainedActionValidator RetainedValidator { get; }

        ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет все релевантные кандидаты действий для текущей точки решения.
        /// </summary>
        void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions);
    }
}
