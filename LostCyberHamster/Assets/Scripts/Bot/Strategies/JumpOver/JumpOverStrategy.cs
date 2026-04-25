using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Собирает компоненты обычного jump-over strategy.
    /// </summary>
    internal sealed class JumpOverStrategy : IPlanningStrategy
    {
        private readonly JumpOverSpecification _specification;
        private readonly JumpClipTravelProvider _travelProvider;
        private readonly JumpOverFireWindowCalculator _fireWindowCalculator;
        private readonly JumpOverSimulator _simulator;

        public JumpOverStrategy()
        {
            _specification = new JumpOverSpecification();
            _travelProvider = new JumpClipTravelProvider("transform_jump");
            _fireWindowCalculator = new JumpOverFireWindowCalculator();
            _simulator = new JumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOverExecutor(triggerGate);
            RetainedValidator = new JumpOutcomeRetainedValidator(ActionKind, _fireWindowCalculator.OutcomeCalculator);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.JumpOver;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            if (actions == null)
                return;

            if (!_specification.IsSatisfiedBy(planningState, decisionPoint, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return;

            if (!_travelProvider.TryGetTravel(out float jumpTravel))
                return;

            if (!_fireWindowCalculator.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    jumpTravel,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, fireShift, jumpTravel));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float jumpTravel)
        {
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.JumpOver,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: JumpOverSpecification.EnergyCost,
                description: $"Jump over {targetObstacle.ObstacleType}");
        }
    }
}
