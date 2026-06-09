using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Собирает role-based кандидаты обычного jump-on-roof.
    /// </summary>
    internal sealed class JumpOnRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpOnRoofPolicy _policy;
        private readonly IBotStrategySpecification _specification;
        private readonly JumpOnRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpOnRoofActionResolver _actionResolver;
        private readonly JumpOnRoofSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты обычного jump-on-roof.
        /// </summary>
        public JumpOnRoofStrategy()
        {
            _policy = new JumpOnRoofPolicy();
            _specification = new JumpOnRoofSpecification(_policy);
            _fireWindowFinder = new JumpOnRoofFireWindowFinder(_policy);
            _actionResolver = new JumpOnRoofActionResolver();
            _simulator = new JumpOnRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOnRoofExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new JumpOnRoofRetainedValidator(_policy, _fireWindowFinder);
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Добавляет jump-on-roof action, если roof support выбран из role-chain и подтвержден resolver-ом.
        /// </summary>
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

            if (!_actionResolver.TryResolve(
                    decisionPoint.Chain,
                    out ObstacleSnapshot targetRoof,
                    out int targetRoofIndex,
                    out int targetRoofChainIndex))
            {
                return;
            }

            if (!_specification.IsSatisfiedBy(planningState, targetRoof))
                return;

            if (!_policy.TryGetTravel(out float jumpTravel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    targetRoof,
                    targetRoofIndex,
                    targetRoofChainIndex,
                    jumpTravel,
                    out JumpOnRoofWindowModel window,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(
                _policy,
                planningState,
                decisionPoint.Chain.FirstObstacle,
                window,
                fireShift,
                jumpTravel));
        }

        /// <summary>
        /// Создает planning action для подтвержденной посадки на крышу.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOnRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            JumpOnRoofWindowModel window,
            float fireShift,
            float jumpTravel)
        {
            ObstacleSnapshot targetRoof = window.TargetObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                window.FirstFireShift,
                window.LastFireShift);

            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                window.TargetObstacleIndex,
                targetObstacleInstanceId: targetRoof.InstanceId,
                triggerObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {targetRoof.ObstacleType}",
                resultRoofSupportInstanceId: targetRoof.InstanceId,
                triggerWindow: triggerWindow);
        }
    }
}
