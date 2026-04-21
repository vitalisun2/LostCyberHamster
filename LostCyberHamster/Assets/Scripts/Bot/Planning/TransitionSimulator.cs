using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.Strategies;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Делегирует симуляцию действий конкретным planning-стратегиям.
    /// </summary>
    public sealed class TransitionSimulator
    {
        private readonly IReadOnlyDictionary<BotActionKind, IPlanningStrategy> _strategiesByActionKind;

        /// <summary>
        /// Создает диспетчер planning-симуляции поверх набора стратегий.
        /// </summary>
        public TransitionSimulator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            var strategiesByActionKind = new Dictionary<BotActionKind, IPlanningStrategy>();
            for (int strategyIndex = 0; strategyIndex < strategies?.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy == null)
                    continue;

                if (strategiesByActionKind.ContainsKey(strategy.ActionKind))
                {
                    throw new InvalidOperationException(
                        $"Для planning-действия зарегистрировано больше одной strategy: kind={strategy.ActionKind}");
                }

                strategiesByActionKind.Add(strategy.ActionKind, strategy);
            }

            _strategiesByActionKind = strategiesByActionKind;
        }

        /// <summary>
        /// Симулирует результат одного запланированного действия.
        /// </summary>
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            IPlanningStrategy strategy = GetRequiredStrategy(action);
            return strategy.Simulate(planningState, action, worldSnapshot);
        }

        private IPlanningStrategy GetRequiredStrategy(PlannedAction action)
        {
            if (action == null)
                throw new InvalidOperationException("План содержит пустое действие для planning-симуляции.");

            if (_strategiesByActionKind.TryGetValue(action.Kind, out IPlanningStrategy strategy))
                return strategy;

            string message =
                $"Для действия бота не зарегистрирована planning-strategy: kind={action.Kind}, desc={action.Description}";

            DebugManager.DiagLog($"[Bot PLAN] ERROR {message}");
            throw new InvalidOperationException(message);
        }
    }
}
