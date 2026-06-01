using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Планирует нулевое ожидание естественного схода с крыши.
    /// </summary>
    internal sealed class PassiveRoofExitStrategy : IPlanningStrategy
    {
        private readonly PassiveRoofExitPolicy _policy;

        public PassiveRoofExitStrategy()
        {
            _policy = new PassiveRoofExitPolicy();
            Executor = new PassiveRoofExitExecutor(_policy);
            RetainedValidator = new PassiveRoofExitRetainedActionValidator(_policy);
            Simulator = new PassiveRoofExitSimulator(_policy);
        }

        /// <summary>
        /// Возвращает тип действия passive roof exit.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        public IActionExecutionHandler Executor { get; }

        public IRetainedActionValidator RetainedValidator { get; }

        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет passive roof exit candidate, когда впереди уже найден required decision.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            if (actions == null)
                return;

            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return;

            if (!PassiveRoofExitPlanner.TryBuildModel(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    runFromRoofTravel,
                    out PassiveRoofExitModel model))
            {
                return;
            }

            actions.Add(BuildAction(planningState, model));
        }

        private PlannedAction BuildAction(
            PlanningState planningState,
            PassiveRoofExitModel model)
        {
            float triggerX = model.LastRoof.LeftX + planningState.ProjectionWorldShift;
            string description = $"{_policy.DescriptionPrefix} before {model.ContextObstacle.ObstacleType}";

            return new PlannedAction(
                _policy.ActionKind,
                triggerX,
                triggerX,
                model.CompletionWorldShift,
                model.CompletionWorldShift,
                model.ContextObstacleIndex,
                targetObstacleInstanceId: model.ContextObstacle.InstanceId,
                triggerObstacleInstanceId: model.LastRoof.InstanceId,
                energyCost: 0,
                description: description);
        }
    }
}
