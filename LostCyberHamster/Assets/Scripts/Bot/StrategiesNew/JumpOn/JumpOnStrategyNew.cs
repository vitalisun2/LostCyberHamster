using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.Bot.StrategiesNew.Shared.JumpOn;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.StrategiesNew.JumpOn
{
    /// <summary>
    /// Собирает role-based кандидаты обычного ground jump-on.
    /// </summary>
    internal sealed class JumpOnStrategyNew : IPlanningStrategyNew
    {
        private readonly IJumpOnPolicy _policy;
        private readonly IBotStrategySpecification _specification;
        private readonly JumpOnFireWindowFinderNew _fireWindowFinder;
        private readonly JumpOnActionChainResolver _actionChainResolver;
        private readonly JumpOnSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты обычного jump-on.
        /// </summary>
        public JumpOnStrategyNew()
        {
            _policy = new JumpOnPolicy();
            _specification = new JumpOnSpecificationNew(_policy);
            _fireWindowFinder = new JumpOnFireWindowFinderNew(_policy);
            _actionChainResolver = new JumpOnActionChainResolver();
            _simulator = new JumpOnSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOnExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new JumpOnRetainedValidatorNew(_policy, _fireWindowFinder);
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }
        public IRetainedActionValidatorNew RetainedValidator { get; }

        /// <summary>
        /// Добавляет обычный jump-on action, если role-based target и полное действие безопасны.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPointNew decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет обязательные аргументы.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Получает runtime travel и строит action-chain до достижимого target.
            if (!_policy.TryGetTravel(out JumpOnTravel travel))
                return;

            if (!_actionChainResolver.TryResolve(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out ObstacleChainNew actionChain,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex))
            {
                return;
            }

            // Проверяет применимость strategy к выбранному target.
            if (!_specification.IsSatisfiedBy(planningState, targetObstacle))
                return;

            // Подтверждает fire window через runtime resolver.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    actionChain,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    travel,
                    out JumpOnWindowModel window,
                    out float fireShift))
            {
                return;
            }

            // Проверяет безопасность после полного завершения.
            float completionWorldShift = fireShift + travel.ActionTravel;
            if (!TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                    planningState,
                    worldSnapshot,
                    window.TargetObstacleIndex,
                    window.TargetObstacle.InstanceId,
                    completionWorldShift))
            {
                return;
            }

            // Добавляет safe action в общий набор кандидатов без локального ранжирования.
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
        /// Создаёт planning action для обычного jump-on с привязкой к trigger и target obstacle.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOnPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            JumpOnWindowModel window,
            float fireShift,
            JumpOnTravel travel,
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

            // Создаёт action без локального сравнения с super-вариантом.
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
