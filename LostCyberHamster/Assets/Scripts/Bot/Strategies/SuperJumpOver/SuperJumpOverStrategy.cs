using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Собирает компоненты super jump-over strategy.
    /// </summary>
    internal sealed class SuperJumpOverStrategy : IPlanningStrategy
    {
        private readonly IJumpOverPolicy _policy;
        private readonly JumpOverSpecification _specification;
        private readonly JumpOverFireWindowFinder _fireWindowFinder;
        private readonly JumpOverSimulator _simulator;

        public SuperJumpOverStrategy()
        {
            _policy = new SuperJumpOverPolicy();
            _specification = new JumpOverSpecification(_policy);
            _fireWindowFinder = new JumpOverFireWindowFinder(_policy);
            _simulator = new JumpOverSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOverExecutor(triggerGate);
            RetainedValidator = new JumpOverRetainedActionValidator(_policy, _fireWindowFinder);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            if (!_specification.IsSatisfiedBy(
                    planningState,
                    decisionPoint,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex))
            {
                return;
            }

            if (!_policy.TryGetTravel(out float superJumpTravel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    superJumpTravel,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(_policy, planningState, targetObstacle, targetObstacleIndex, fireShift, superJumpTravel));
        }

        private static PlannedAction BuildAction(
            IJumpOverPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float superJumpTravel)
        {
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {targetObstacle.ObstacleType}");
        }
    }
}
