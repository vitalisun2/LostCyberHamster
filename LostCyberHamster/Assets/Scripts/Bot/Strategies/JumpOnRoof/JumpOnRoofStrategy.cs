using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Собирает компоненты jump-on-roof strategy.
    /// </summary>
    internal sealed class JumpOnRoofStrategy : IPlanningStrategy
    {
        private readonly JumpOnRoofSpecification _specification;
        private readonly JumpClipTravelProvider _travelProvider;
        private readonly JumpOnRoofFireWindowCalculator _fireWindowCalculator;
        private readonly JumpOnRoofSimulator _simulator;

        public JumpOnRoofStrategy()
        {
            _specification = new JumpOnRoofSpecification();
            _travelProvider = new JumpClipTravelProvider("transform_jump");
            _fireWindowCalculator = new JumpOnRoofFireWindowCalculator();
            _simulator = new JumpOnRoofSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOnRoofExecutor(triggerGate);
            RetainedValidator = new JumpOutcomeRetainedValidator(ActionKind, _fireWindowCalculator.OutcomeCalculator);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;
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
                BotActionKind.JumpOnRoof,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: JumpOnRoofSpecification.EnergyCost,
                description: $"Jump on roof {targetObstacle.ObstacleType}");
        }
    }
}
