using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperRoofJumpOver
{
    /// <summary>
    /// Строит действия super roof jump-over над опасными obstacle на текущей крыше.
    /// </summary>
    internal sealed class SuperRoofJumpOverStrategy : IPlanningStrategy
    {
        private readonly IRoofJumpOverPolicy _policy;
        private readonly RoofJumpOverSpecification _specification;
        private readonly RoofJumpOverFireWindowFinder _fireWindowFinder;
        private readonly RoofJumpOverSimulator _simulator;

        public SuperRoofJumpOverStrategy()
        {
            _policy = new SuperRoofJumpOverPolicy();
            _specification = new RoofJumpOverSpecification(_policy);
            _fireWindowFinder = new RoofJumpOverFireWindowFinder(_policy);
            _simulator = new RoofJumpOverSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperRoofJumpOverExecutor(triggerGate);
            RetainedValidator = new RoofJumpOverRetainedActionValidator(_policy, _fireWindowFinder);
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
                    worldSnapshot,
                    decisionPoint,
                    out _,
                    out _))
                return;

            if (!_policy.TryGetTravel(out RoofJumpOverTravel travel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out RoofJumpOverChainModel chainModel,
                    out ObstacleSnapshot supportObstacle,
                    out float fireShift))
                return;

            actions.Add(BuildAction(
                _policy,
                planningState,
                chainModel,
                supportObstacle,
                fireShift,
                travel));
        }

        private static PlannedAction BuildAction(
            IRoofJumpOverPolicy policy,
            PlanningState planningState,
            RoofJumpOverChainModel chainModel,
            ObstacleSnapshot supportObstacle,
            float fireShift,
            RoofJumpOverTravel travel)
        {
            ObstacleSnapshot hazardObstacle = chainModel.FirstHazard;
            float projectedTriggerX = hazardObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + travel.RoofJumpTravel,
                postFireWorldShift: travel.RoofJumpTravel,
                chainModel.LastHazardIndex,
                targetObstacleInstanceId: hazardObstacle.InstanceId,
                triggerObstacleInstanceId: hazardObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: BuildDescription(policy, chainModel),
                resultRoofSupportInstanceId: supportObstacle.InstanceId);
        }

        private static string BuildDescription(
            IRoofJumpOverPolicy policy,
            RoofJumpOverChainModel chainModel)
        {
            string baseDescription = $"{policy.DescriptionPrefix} {chainModel.FirstHazard.ObstacleType}";
            return chainModel.HazardCount <= 1
                ? baseDescription
                : $"{baseDescription} x{chainModel.HazardCount}";
        }
    }
}