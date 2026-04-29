using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Делегирует симуляцию действий конкретным planning-стратегиям.
    /// </summary>
    public sealed class TransitionSimulator
    {
        private readonly IReadOnlyDictionary<BotActionKind, ISimulator> _simulatorsByActionKind;

        /// <summary>
        /// Создает диспетчер planning-симуляции поверх набора стратегий.
        /// </summary>
        internal TransitionSimulator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            var simulatorsByActionKind = new Dictionary<BotActionKind, ISimulator>();
            for (int strategyIndex = 0; strategyIndex < strategies?.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy?.Simulator == null)
                    continue;

                if (simulatorsByActionKind.ContainsKey(strategy.ActionKind))
                {
                    throw new InvalidOperationException(
                        $"Для planning-действия зарегистрировано больше одного simulator: kind={strategy.ActionKind}");
                }

                simulatorsByActionKind.Add(strategy.ActionKind, strategy.Simulator);
            }

            _simulatorsByActionKind = simulatorsByActionKind;
        }

        /// <summary>
        /// Симулирует результат одного запланированного действия.
        /// </summary>
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            ISimulator simulator = GetRequiredSimulator(action);
            return simulator.Simulate(planningState, action, worldSnapshot);
        }

        private ISimulator GetRequiredSimulator(PlannedAction action)
        {
            if (action == null)
                throw new InvalidOperationException("План содержит пустое действие для planning-симуляции.");

            if (_simulatorsByActionKind.TryGetValue(action.Kind, out ISimulator simulator))
                return simulator;

            string message =
                $"Для действия бота не зарегистрирован simulator: kind={action.Kind}, desc={action.Description}";

            DebugManager.DiagLog($"[Bot PLAN] ERROR {message}");
            throw new InvalidOperationException(message);
        }
    }
}
