using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

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
        /// Пытается сгенерировать действие для текущей точки решения.
        /// </summary>
        bool TryGenerate(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out PlannedAction action);

        /// <summary>
        /// Симулирует planning-результат успешного выполнения действия.
        /// </summary>
        PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot);
    }
}
