using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Собирает role-based кандидаты super-jump-on-roof.
    /// </summary>
    internal sealed class SuperJumpOnRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpOnRoofPolicy _policy;
        private readonly IActionSubjectSpecification _specification;
        private readonly JumpOnRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpOnRoofActionResolver _actionResolver;
        private readonly JumpOnRoofSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты super-jump-on-roof.
        /// </summary>
        public SuperJumpOnRoofStrategy()
        {
            _policy = new SuperJumpOnRoofPolicy();
            _specification = new JumpOnRoofSpecification();
            _fireWindowFinder = new JumpOnRoofFireWindowFinder(_policy);
            _actionResolver = new JumpOnRoofActionResolver();
            _simulator = new JumpOnRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOnRoofExecutor(triggerGate);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет, есть ли ground-ситуация с roof support для super-прыжка на крышу.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.IsGroundRunCurrentLane(planningState, decisionPoint)
                && PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.RoofSupport);
        }

        /// <summary>
        /// Добавляет super-jump-on-roof action, если roof support выбран и подтвержден resolver-ом.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            if (!_actionResolver.TryResolve(
                    decisionPoint.Chain,
                    out ObstacleSnapshot targetRoof,
                    out int targetRoofIndex,
                    out int targetRoofChainIndex))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            if (!_specification.IsSubjectValid(planningState, targetRoof))
                return PlanningStrategyResult.NotApplicable();

            // Проверяет ресурс применимой strategy до поиска safe-window.
            if (planningState.Hamster.Energy < _policy.EnergyCost)
            {
                return PlanningStrategyResult.InsufficientEnergy(
                    nameof(SuperJumpOnRoofStrategy),
                    _policy.ActionKind,
                    _policy.EnergyCost,
                    planningState.Hamster.Energy);
            }

            if (!_policy.TryGetTravel(out float superJumpTravel))
                return PlanningStrategyResult.NotApplicable();

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    targetRoof,
                    targetRoofIndex,
                    targetRoofChainIndex,
                    superJumpTravel,
                    out JumpOnRoofWindowModel window,
                    out float fireShift,
                    out string deadEndReason))
            {
                return DeadEnd(deadEndReason);
            }

            return PlanningStrategyResult.FromAction(BuildAction(
                _policy,
                planningState,
                decisionPoint.Chain.FirstObstacle,
                window,
                fireShift,
                superJumpTravel));
        }

        /// <summary>
        /// Создает dead-end результат для применимой super-jump-on-roof strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(SuperJumpOnRoofStrategy), message);
        }

        /// <summary>
        /// Создает planning action для подтвержденной super-посадки на крышу.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOnRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            JumpOnRoofWindowModel window,
            float fireShift,
            float superJumpTravel)
        {
            ObstacleSnapshot targetRoof = window.TargetObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                window.FirstFireShift,
                window.LastFireShift);

            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
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
