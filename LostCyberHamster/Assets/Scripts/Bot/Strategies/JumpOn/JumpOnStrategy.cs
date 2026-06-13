using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpOn;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpOn
{
    /// <summary>
    /// Собирает role-based кандидаты обычного ground jump-on.
    /// </summary>
    internal sealed class JumpOnStrategy : IPlanningStrategy
    {
        private readonly IJumpOnPolicy _policy;
        private readonly IBotStrategySpecification _specification;
        private readonly JumpOnFireWindowFinder _fireWindowFinder;
        private readonly JumpOnActionChainResolver _actionChainResolver;
        private readonly JumpOnSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты обычного jump-on.
        /// </summary>
        public JumpOnStrategy()
        {
            _policy = new JumpOnPolicy();
            _specification = new JumpOnSpecification(_policy);
            _fireWindowFinder = new JumpOnFireWindowFinder(_policy);
            _actionChainResolver = new JumpOnActionChainResolver();
            _simulator = new JumpOnSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOnExecutor(triggerGate);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет обычный jump-on action, если role-based target и полное действие безопасны.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            // Проверяет обязательные аргументы.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            // Получает runtime travel и строит action-chain до достижимого target.
            if (!_policy.TryGetTravel(out JumpOnTravel travel))
                return PlanningStrategyResult.NotApplicable();

            if (!_actionChainResolver.TryResolve(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out ObstacleChain actionChain,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex,
                    out string resolveDeadEndReason))
            {
                return string.IsNullOrEmpty(resolveDeadEndReason)
                    ? PlanningStrategyResult.NotApplicable()
                    : DeadEnd(resolveDeadEndReason);
            }

            // Проверяет применимость strategy к выбранному target.
            if (!_specification.IsSatisfiedBy(planningState, targetObstacle))
                return PlanningStrategyResult.NotApplicable();

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
                    out float fireShift,
                    out string fireWindowDeadEndReason))
            {
                return DeadEnd(fireWindowDeadEndReason);
            }

            // Проверяет безопасность после полного завершения.
            float completionWorldShift = fireShift + travel.ActionTravel;
            if (!TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                    planningState,
                    worldSnapshot,
                    window.TargetObstacleIndex,
                    window.TargetObstacle.InstanceId,
                    completionWorldShift,
                    out string postActionDeadEndReason))
            {
                return DeadEnd(postActionDeadEndReason);
            }

            // Добавляет safe action в общий набор кандидатов без локального ранжирования.
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
        /// Создает dead-end результат для применимой jump-on strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(JumpOnStrategy), message);
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
            float triggerX = projectedTriggerX;
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
