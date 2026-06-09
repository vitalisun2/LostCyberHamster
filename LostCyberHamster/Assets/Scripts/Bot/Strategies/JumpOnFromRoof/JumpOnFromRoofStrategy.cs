using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpOnFromRoof
{
    /// <summary>
    /// Собирает role-based кандидаты обычного напрыгивания с крыши на дорожный target.
    /// </summary>
    internal sealed class JumpOnFromRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy обычного roof-to-road jump-on.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        /// <summary>
        /// Specification применимости обычного roof-to-road jump-on к выбранному target.
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
        /// Simulator planning-перехода обычного roof-to-road jump-on.
        /// </summary>
        private readonly JumpOnFromRoofSimulator _simulator;

        public JumpOnFromRoofStrategy()
        {
            // Инициализирует planning-компоненты стратегии.
            _policy = new JumpOnFromRoofPolicy();
            _specification = new JumpOnFromRoofSpecification(_policy);
            _actionResolver = new JumpOnFromRoofActionResolver();
            _fireWindowFinder = new JumpOnFromRoofFireWindowFinder(_policy);
            _simulator = new JumpOnFromRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует runtime handlers наружу.
            Executor = new JumpOnFromRoofExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new JumpOnFromRoofRetainedValidator(
                _policy,
                _fireWindowFinder,
                _specification);
        }

        /// <summary>
        /// Тип action, который создает стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor обычного roof-to-road jump-on.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator обычного roof-to-road jump-on.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Validator сохраненных actions обычного roof-to-road jump-on.
        /// </summary>
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Добавляет ordinary roof-to-road jump-on action, если target и полное действие безопасны.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет обязательные аргументы и runtime travel.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            if (!_policy.TryGetTravel(out JumpOnFromRoofTravel travel))
                return;

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
                    out ObstacleSnapshot lastRoof))
            {
                return;
            }

            if (!_specification.IsSatisfiedBy(planningState, targetObstacle))
                return;

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
                    out float fireShift))
            {
                return;
            }

            float completionWorldShift = fireShift + travel.ActionTravel;

            // Добавляет safe action без локального сравнения с super-вариантом.
            actions.Add(BuildAction(
                _policy,
                planningState,
                actionChain.FirstObstacle,
                window,
                fireShift,
                travel,
                completionWorldShift));
        }

        /// <summary>
        /// Создает planning action для обычного roof-to-road jump-on.
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
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
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
