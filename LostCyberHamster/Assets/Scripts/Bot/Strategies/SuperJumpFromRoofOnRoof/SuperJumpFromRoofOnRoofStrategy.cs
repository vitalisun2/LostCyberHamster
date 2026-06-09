using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Собирает role-based кандидаты super-прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class SuperJumpFromRoofOnRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy super roof-to-roof прыжка.
        /// </summary>
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        /// <summary>
        /// Specification применимости super roof-to-roof прыжка.
        /// </summary>
        private readonly JumpFromRoofOnRoofSpecification _specification;

        /// <summary>
        /// Finder fire-window с runtime-проверкой target roof.
        /// </summary>
        private readonly JumpFromRoofOnRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Simulator planning-перехода super roof-to-roof прыжка.
        /// </summary>
        private readonly JumpFromRoofOnRoofSimulator _simulator;

        public SuperJumpFromRoofOnRoofStrategy()
        {
            // Инициализирует зависимости стратегии.
            _policy = new SuperJumpFromRoofOnRoofPolicy();
            _specification = new JumpFromRoofOnRoofSpecification(_policy);
            _fireWindowFinder = new JumpFromRoofOnRoofFireWindowFinder(_policy);
            _simulator = new JumpFromRoofOnRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует обработчики и симулятор наружу.
            Executor = new SuperJumpFromRoofOnRoofExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new JumpFromRoofOnRoofRetainedValidator(
                _policy,
                _fireWindowFinder,
                _specification);
        }

        /// <summary>
        /// Тип action, который создает стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor super roof-to-roof прыжка.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator super roof-to-roof прыжка.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Validator сохраненных actions super roof-to-roof прыжка.
        /// </summary>
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Добавляет super roof-to-roof action, если passive roof exit опасен и следующая roof подтверждена.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет обязательный вход.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Проверяет применимость strategy.
            if (!_specification.IsSatisfiedBy(planningState))
                return;

            // Получает runtime-дистанции.
            if (!_policy.TryGetTravel(out JumpFromRoofOnRoofTravel travel))
                return;

            // Ищет target roof и подбирает fire shift.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out ObstacleSnapshot targetRoof,
                    out int targetRoofIndex,
                    out float firstFireShift,
                    out float lastFireShift,
                    out float fireShift))
            {
                return;
            }

            // Добавляет planned action без локального фильтра against ordinary.
            actions.Add(BuildAction(
                _policy,
                planningState,
                targetRoof,
                targetRoofIndex,
                firstFireShift,
                lastFireShift,
                fireShift,
                travel));
        }

        /// <summary>
        /// Создает planned action для найденного super roof-to-roof fire shift.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpFromRoofOnRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot targetRoof,
            int targetRoofIndex,
            float firstFireShift,
            float lastFireShift,
            float fireShift,
            JumpFromRoofOnRoofTravel travel)
        {
            // Считает trigger position по target roof.
            float projectedTriggerX = targetRoof.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                firstFireShift,
                lastFireShift);

            // Возвращает action с target roof как execution anchor.
            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + travel.RoofJumpTravel,
                postFireWorldShift: travel.RoofJumpTravel,
                targetRoofIndex,
                targetObstacleInstanceId: targetRoof.InstanceId,
                triggerObstacleInstanceId: targetRoof.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {targetRoof.ObstacleType}",
                resultRoofSupportInstanceId: targetRoof.InstanceId,
                triggerWindow: triggerWindow);
        }
    }
}
