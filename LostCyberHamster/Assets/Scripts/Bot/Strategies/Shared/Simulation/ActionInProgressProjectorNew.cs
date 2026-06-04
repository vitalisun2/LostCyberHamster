using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;

namespace Assets.Scripts.Bot.Strategies.Shared.Simulation
{
    /// <summary>
    /// Диспетчеризует projection незавершённых role-based head-действий по strategy.
    /// </summary>
    public sealed class ActionInProgressProjectorNew
    {
        private readonly IReadOnlyDictionary<BotActionKind, ISimulator> _simulatorsByKind;

        internal ActionInProgressProjectorNew(IReadOnlyList<IPlanningStrategyNew> strategies)
        {
            var simulatorsByKind = new Dictionary<BotActionKind, ISimulator>();
            for (int strategyIndex = 0; strategyIndex < strategies?.Count; strategyIndex++)
            {
                IPlanningStrategyNew strategy = strategies[strategyIndex];
                if (strategy?.Simulator == null)
                    continue;

                if (simulatorsByKind.ContainsKey(strategy.ActionKind))
                {
                    throw new InvalidOperationException(
                        $"Для role-based strategy зарегистрировано больше одного simulator: kind={strategy.ActionKind}");
                }

                simulatorsByKind.Add(strategy.ActionKind, strategy.Simulator);
            }

            _simulatorsByKind = simulatorsByKind;
        }

        /// <summary>
        /// Проецирует незавершённое role-based действие до planning boundary.
        /// </summary>
        public PlanningState Project(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            if (_simulatorsByKind.TryGetValue(action.Kind, out ISimulator simulator))
                return simulator.ProjectInProgress(planningState, action, worldSnapshot);

            string message =
                $"Для role-based действия бота не зарегистрирован simulator: kind={action.Kind}, desc={action.Description}";
            throw new InvalidOperationException(message);
        }
    }
}
