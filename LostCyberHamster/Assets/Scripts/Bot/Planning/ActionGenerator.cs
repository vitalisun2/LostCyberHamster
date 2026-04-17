using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.Strategies;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Собирает все действия, доступные для текущей точки решения.
    /// </summary>
    public sealed class ActionGenerator
    {
        private readonly IReadOnlyList<IPlanningStrategy> _strategies;
        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();

        /// <summary>
        /// Создает генератор действий поверх набора planning-стратегий.
        /// </summary>
        public ActionGenerator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategy>();
        }

        /// <summary>
        /// Генерирует кандидатов действий для текущего planning-состояния.
        /// </summary>
        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            var plannedActions = new List<PlannedAction>();
            if (planningState == null || worldSnapshot == null)
                return plannedActions;

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (!_decisionPointDetector.TryDetect(planningState, projectedWorldSnapshot, out DecisionPoint decisionPoint))
                return plannedActions;

            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                if (_strategies[strategyIndex].TryGenerate(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint,
                    out PlannedAction action))
                {
                    plannedActions.Add(action);
                }
            }

            return plannedActions;
        }
    }
}
