using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Собирает компоненты super jump-on-roof strategy.
    /// </summary>
    internal sealed class SuperJumpOnRoofStrategy : IPlanningStrategy
    {
        private readonly SuperJumpOnRoofSpecification _specification;
        private readonly JumpClipTravelProvider _travelProvider;
        private readonly SuperJumpOnRoofFireWindowCalculator _fireWindowCalculator;
        private readonly SuperJumpOnRoofSimulator _simulator;

        public SuperJumpOnRoofStrategy()
        {
            _specification = new SuperJumpOnRoofSpecification();
            _travelProvider = new JumpClipTravelProvider(
                "transform_super_jump",
                DoubleJumpDetector.DoubleJumpThreshold * 0.5f * Assets.Scripts.Consts.GameSpeedBase,
                throwIfMissing: true);
            _fireWindowCalculator = new SuperJumpOnRoofFireWindowCalculator();
            _simulator = new SuperJumpOnRoofSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOnRoofExecutor(triggerGate);
            RetainedValidator = new JumpOutcomeRetainedValidator(ActionKind, _fireWindowCalculator.OutcomeCalculator);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SuperJumpOnRoof;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            if (!_specification.IsSatisfiedBy(planningState, decisionPoint, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return;

            if (!_travelProvider.TryGetTravel(out float superJumpTravel))
                return;

            if (!_fireWindowCalculator.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    superJumpTravel,
                    preferLatestFireShift: decisionPoint.Kind == DecisionPointKind.BlockingObstacleWithRoofLanding,
                    out float fireShift))
            {
                return;
            }

            ObstacleSnapshot triggerObstacle = decisionPoint.Obstacle ?? targetObstacle;
            actions.Add(BuildAction(planningState, triggerObstacle, targetObstacle, targetObstacleIndex, fireShift, superJumpTravel));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float superJumpTravel)
        {
            float triggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.SuperJumpOnRoof,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: SuperJumpOnRoofSpecification.EnergyCost,
                description: $"Super jump on roof {targetObstacle.ObstacleType}");
        }
    }
}