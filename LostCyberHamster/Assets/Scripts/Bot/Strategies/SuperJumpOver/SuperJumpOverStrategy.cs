using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Собирает компоненты super jump-over strategy.
    /// </summary>
    internal sealed class SuperJumpOverStrategy : IPlanningStrategy
    {
        private readonly SuperJumpOverSpecification _specification;
        private readonly JumpClipTravelProvider _travelProvider;
        private readonly SuperJumpOverFireWindowCalculator _fireWindowCalculator;
        private readonly SuperJumpOverSimulator _simulator;

        public SuperJumpOverStrategy()
        {
            _specification = new SuperJumpOverSpecification();
            _travelProvider = new JumpClipTravelProvider(
                "transform_super_jump",
                DoubleJumpDetector.DoubleJumpThreshold * 0.5f * Assets.Scripts.Consts.GameSpeedBase,
                throwIfMissing: true);
            _fireWindowCalculator = new SuperJumpOverFireWindowCalculator();
            _simulator = new SuperJumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOverExecutor(triggerGate);
            RetainedValidator = new JumpOutcomeRetainedValidator(ActionKind, _fireWindowCalculator.OutcomeCalculator);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SuperJumpOver;
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
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, fireShift, superJumpTravel));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float superJumpTravel)
        {
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.SuperJumpOver,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: SuperJumpOverSpecification.EnergyCost,
                description: $"Super jump over {targetObstacle.ObstacleType}");
        }
    }
}
