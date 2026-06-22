using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnFromRoof
{
    /// <summary>
    /// Собирает role-based кандидаты super-напрыгивания с крыши на дорожный target.
    /// </summary>
    internal sealed class SuperJumpOnFromRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy super roof-to-road jump-on.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        /// <summary>
        /// Specification применимости super roof-to-road jump-on к выбранному target.
        /// </summary>
        private readonly JumpOnFromRoofSpecification _specification;

        /// <summary>
        /// Resolver target и roof context внутри role-based chain.
        /// </summary>
        private readonly JumpOnFromRoofActionResolver _actionResolver;

        /// <summary>
        /// Finder fire-window с runtime-проверкой target.
        /// </summary>
        private readonly JumpOnFromRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Simulator planning-перехода super roof-to-road jump-on.
        /// </summary>
        private readonly JumpOnFromRoofSimulator _simulator;

        public SuperJumpOnFromRoofStrategy()
        {
            // Инициализирует planning-компоненты стратегии.
            _policy = new SuperJumpOnFromRoofPolicy();
            _specification = new JumpOnFromRoofSpecification();
            _actionResolver = new JumpOnFromRoofActionResolver();
            _fireWindowFinder = new JumpOnFromRoofFireWindowFinder(_policy);
            _simulator = new JumpOnFromRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует runtime handlers наружу.
            Executor = new SuperJumpOnFromRoofExecutor(triggerGate);
            Simulator = _simulator;
        }

        /// <summary>
        /// Тип action, который создает стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor super roof-to-road jump-on.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator super roof-to-road jump-on.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет roof-run context перед поиском super roof-to-road target.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.IsRoofRunCurrentLane(planningState, decisionPoint);
        }

        /// <summary>
        /// Добавляет super roof-to-road jump-on action, если target и полное действие безопасны.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            // Проверяет обязательные аргументы и runtime travel.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            if (!_policy.TryGetTravel(out JumpOnFromRoofTravel travel))
                return PlanningStrategyResult.NotApplicable();

            // Выбирает target внутри role-based chain.
            if (!_actionResolver.TryResolve(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out ObstacleChain actionChain,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex,
                    out ObstacleSnapshot lastRoof,
                    out string resolveDeadEndReason))
            {
                return string.IsNullOrEmpty(resolveDeadEndReason)
                    ? PlanningStrategyResult.NotApplicable()
                    : DeadEnd(resolveDeadEndReason);
            }

            if (!_specification.IsSubjectValid(planningState, targetObstacle))
                return PlanningStrategyResult.NotApplicable();

            // Проверяет ресурс применимой strategy до поиска safe-window.
            if (planningState.Hamster.Energy < _policy.EnergyCost)
            {
                return PlanningStrategyResult.InsufficientEnergy(
                    nameof(SuperJumpOnFromRoofStrategy),
                    _policy.ActionKind,
                    _policy.EnergyCost,
                    planningState.Hamster.Energy);
            }

            // Подтверждает fire-window через runtime resolver.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    actionChain,
                    travel,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    lastRoof,
                    out JumpOnFromRoofWindowModel window,
                    out float fireShift,
                    out string deadEndReason))
            {
                return DeadEnd(deadEndReason);
            }

            float completionWorldShift = fireShift + travel.ActionTravel;

            // Добавляет safe action без локального сравнения с ordinary-вариантом.
            return PlanningStrategyResult.FromAction(BuildAction(
                _policy,
                planningState,
                actionChain.FirstObstacle,
                window,
                fireShift,
                travel,
                completionWorldShift));
        }

        /// <summary>
        /// Создает dead-end результат для применимой super-jump-on-from-roof strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(SuperJumpOnFromRoofStrategy), message);
        }

        /// <summary>
        /// Создает planning action для super roof-to-road jump-on.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOnFromRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            JumpOnFromRoofWindowModel window,
            float fireShift,
            JumpOnFromRoofTravel travel,
            float completionWorldShift)
        {
            // Вычисляет координату запуска.
            ObstacleSnapshot targetObstacle = window.TargetObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                window.FirstFireShift,
                window.LastFireShift);

            // Создает action с target как удаляемым obstacle.
            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift,
                postFireWorldShift: travel.ActionTravel,
                window.TargetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {targetObstacle.ObstacleType}",
                fulfillsJumpOnObjective: JumpOnObjectiveRules.HasEnergyForJumpOnObjective(planningState.Hamster),
                triggerWindow: triggerWindow);
        }
    }
}
