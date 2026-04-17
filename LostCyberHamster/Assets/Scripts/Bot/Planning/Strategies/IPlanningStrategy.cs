using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    /// <summary>
    /// Описывает planning-стратегию для одного семейства действий бота.
    /// </summary>
    public interface IPlanningStrategy
    {
        /// <summary>
        /// Возвращает тип действия, который обслуживает стратегия.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Добавляет все релевантные кандидаты действий для текущей точки решения.
        /// </summary>
        void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions);

        /// <summary>
        /// Симулирует planning-результат успешного выполнения действия.
        /// </summary>
        PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot);
    }
}
