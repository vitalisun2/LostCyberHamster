using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpOn
{
    /// <summary>
    /// Собирает компоненты обычного ground jump-on strategy.
    /// </summary>
    internal sealed class JumpOnStrategy : IPlanningStrategy
    {
        private readonly IJumpOnPolicy _policy;
        private readonly JumpOnSpecification _specification;
        private readonly JumpOnFireWindowFinder _fireWindowFinder;
        private readonly JumpOnSimulator _simulator;

        public JumpOnStrategy()
        {
            _policy = new JumpOnPolicy();
            _specification = new JumpOnSpecification(_policy);
            _fireWindowFinder = new JumpOnFireWindowFinder(_policy);
            _simulator = new JumpOnSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOnExecutor(triggerGate);
            RetainedValidator = new JumpOnRetainedActionValidator(_policy, _fireWindowFinder);
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
                    out _,
                    out _))
            {
                return;
            }

            if (!_policy.TryGetTravel(out float jumpTravel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    jumpTravel,
                    out JumpOnWindowModel window,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(_policy, planningState, window, fireShift, jumpTravel));
        }

        private static PlannedAction BuildAction(
            IJumpOnPolicy policy,
            PlanningState planningState,
            JumpOnWindowModel window,
            float fireShift,
            float jumpTravel)
        {
            ObstacleSnapshot targetObstacle = window.TargetObstacle;
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                window.TargetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {targetObstacle.ObstacleType}");
        }
    }
}
