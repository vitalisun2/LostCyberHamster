using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpFromRoofOnRoof
{
    /// <summary>
    /// Собирает role-based кандидаты обычного прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy обычного roof-to-roof прыжка.
        /// </summary>
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        /// <summary>
        /// Specification применимости обычного roof-to-roof прыжка.
        /// </summary>
        private readonly JumpFromRoofOnRoofSpecification _specification;

        /// <summary>
        /// Finder fire-window с runtime-проверкой target roof.
        /// </summary>
        private readonly JumpFromRoofOnRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Simulator planning-перехода обычного roof-to-roof прыжка.
        /// </summary>
        private readonly JumpFromRoofOnRoofSimulator _simulator;

        public JumpFromRoofOnRoofStrategy()
        {
            // Инициализирует зависимости стратегии.
            _policy = new JumpFromRoofOnRoofPolicy();
            _specification = new JumpFromRoofOnRoofSpecification();
            _fireWindowFinder = new JumpFromRoofOnRoofFireWindowFinder(_policy);
            _simulator = new JumpFromRoofOnRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует обработчики и симулятор наружу.
            Executor = new JumpFromRoofOnRoofExecutor(triggerGate);
            Simulator = _simulator;
        }

        /// <summary>
        /// Тип action, который создает стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor обычного roof-to-roof прыжка.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator обычного roof-to-roof прыжка.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет roof-run ситуацию, где passive roof exit блокируется угрозой.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.IsRoofRunCurrentLane(planningState, decisionPoint)
                && PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.BlockingThreat);
        }

        /// <summary>
        /// Добавляет roof-to-roof action, если passive roof exit опасен и следующая roof подтверждена.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            // Проверяет обязательный вход.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            // Проверяет применимость strategy.
            if (!_specification.IsStateValid(planningState))
                return PlanningStrategyResult.NotApplicable();

            // Проверяет ресурс применимой strategy до поиска safe-window.
            if (planningState.Hamster.Energy < _policy.EnergyCost)
            {
                return PlanningStrategyResult.InsufficientEnergy(
                    nameof(JumpFromRoofOnRoofStrategy),
                    _policy.ActionKind,
                    _policy.EnergyCost,
                    planningState.Hamster.Energy);
            }

            // Получает runtime-дистанции.
            if (!_policy.TryGetTravel(out JumpFromRoofOnRoofTravel travel))
                return PlanningStrategyResult.NotApplicable();

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
                    out float fireShift,
                    out string deadEndReason))
            {
                return string.IsNullOrEmpty(deadEndReason)
                    ? PlanningStrategyResult.NotApplicable()
                    : DeadEnd(deadEndReason);
            }

            // Добавляет planned action.
            return PlanningStrategyResult.FromAction(BuildAction(
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
        /// Создает dead-end результат для применимой roof-to-roof strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(JumpFromRoofOnRoofStrategy), message);
        }

        /// <summary>
        /// Создает planned action для найденного roof-to-roof fire shift.
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
            float triggerX = projectedTriggerX;
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
