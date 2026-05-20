using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Strategies.Shared.Simulation
{
    /// <summary>
    /// Диспетчеризует projection незавершённых head-действий по strategy.
    /// </summary>
    public sealed class ActionInProgressProjector
    {
        private readonly IReadOnlyDictionary<BotActionKind, ISimulator> _simulatorsByKind;

        internal ActionInProgressProjector(IReadOnlyList<IPlanningStrategy> strategies)
        {
            var simulatorsByKind = new Dictionary<BotActionKind, ISimulator>();
            for (int strategyIndex = 0; strategyIndex < strategies?.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy?.Simulator == null)
                    continue;

                if (simulatorsByKind.ContainsKey(strategy.ActionKind))
                {
                    throw new InvalidOperationException(
                        $"Для strategy зарегистрировано больше одного simulator: kind={strategy.ActionKind}");
                }

                simulatorsByKind.Add(strategy.ActionKind, strategy.Simulator);
            }

            _simulatorsByKind = simulatorsByKind;
        }

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
                $"Для действия бота не зарегистрирован simulator: kind={action.Kind}, desc={action.Description}";
            throw new InvalidOperationException(message);
        }
    }
}
