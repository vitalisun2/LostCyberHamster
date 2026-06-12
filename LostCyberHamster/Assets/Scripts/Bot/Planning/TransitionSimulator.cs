using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Делегирует симуляцию role-based действий конкретным новым planning-стратегиям.
    /// </summary>
    public sealed class TransitionSimulator
    {
        private readonly IReadOnlyDictionary<BotActionKind, ISimulator> _simulatorsByActionKind;

        /// <summary>
        /// Создает диспетчер role-based planning-симуляции поверх набора новых стратегий.
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
                        $"Для role-based planning-действия зарегистрировано больше одного simulator: kind={strategy.ActionKind}");
                }

                simulatorsByActionKind.Add(strategy.ActionKind, strategy.Simulator);
            }

            _simulatorsByActionKind = simulatorsByActionKind;
        }

        /// <summary>
        /// Симулирует результат одного role-based запланированного действия.
        /// </summary>
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            ISimulator simulator = GetRequiredSimulator(action);
            return simulator.Simulate(planningState, action, worldSnapshot);
        }

        /// <summary>
        /// Проецирует результат уже запущенного head-action до его ожидаемого завершения.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            ISimulator simulator = GetRequiredSimulator(action);
            return simulator.ProjectInProgress(
                planningState,
                action,
                worldSnapshot,
                remainingPostFireWorldShift);
        }

        /// <summary>
        /// Возвращает simulator для указанного действия или сообщает ошибку конфигурации нового path.
        /// </summary>
        private ISimulator GetRequiredSimulator(PlannedAction action)
        {
            if (action == null)
                throw new InvalidOperationException("План содержит пустое действие для role-based planning-симуляции.");

            if (_simulatorsByActionKind.TryGetValue(action.Kind, out ISimulator simulator))
                return simulator;

            string message =
                $"Для role-based действия бота не зарегистрирован simulator: kind={action.Kind}, desc={action.Description}";
            throw new InvalidOperationException(message);
        }
    }
}
