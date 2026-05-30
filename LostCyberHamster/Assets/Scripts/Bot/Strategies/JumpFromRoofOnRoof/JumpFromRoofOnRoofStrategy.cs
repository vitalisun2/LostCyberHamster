using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpFromRoofOnRoof
{
    /// <summary>
    /// Собирает действия обычного прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpFromRoofOnRoofPolicy _policy;
        private readonly JumpFromRoofOnRoofSpecification _specification;
        private readonly JumpFromRoofOnRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpFromRoofOnRoofSimulator _simulator;

        public JumpFromRoofOnRoofStrategy()
        {
            // Инициализирует зависимости стратегии.
            _policy = new JumpFromRoofOnRoofPolicy();
            _specification = new JumpFromRoofOnRoofSpecification(_policy);
            _fireWindowFinder = new JumpFromRoofOnRoofFireWindowFinder(_policy);
            _simulator = new JumpFromRoofOnRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует обработчики и симулятор наружу.
            Executor = new JumpFromRoofOnRoofExecutor(triggerGate);
            RetainedValidator = new JumpFromRoofOnRoofRetainedActionValidator(_policy, _fireWindowFinder, _specification);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет возможный roof-to-roof прыжок в список planned actions.
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
            {
                return;
            }

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

            // Добавляет planned action.
            actions.Add(BuildAction(_policy, planningState, targetRoof, targetRoofIndex, firstFireShift, lastFireShift, fireShift, travel));
        }

        /// <summary>
        /// Создает planned action для найденного fire shift.
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
