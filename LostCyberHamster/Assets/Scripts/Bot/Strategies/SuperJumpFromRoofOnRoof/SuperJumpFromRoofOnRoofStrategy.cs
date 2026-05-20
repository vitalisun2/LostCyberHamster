using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Собирает действия super-прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class SuperJumpFromRoofOnRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpFromRoofOnRoofPolicy _policy;
        private readonly JumpFromRoofOnRoofSpecification _specification;
        private readonly JumpFromRoofOnRoofFireWindowFinder _fireWindowFinder;
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
            RetainedValidator = new JumpFromRoofOnRoofRetainedActionValidator(_policy, _fireWindowFinder, _specification);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет возможный super roof-to-roof прыжок в список planned actions.
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
            {
                LogPlanReject(planningState, "spec");
                return;
            }

            // Получает runtime-дистанции.
            if (!_policy.TryGetTravel(out JumpFromRoofOnRoofTravel travel))
            {
                LogPlanReject(planningState, "travel");
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
                    out float fireShift))
            {
                LogPlanReject(planningState, "fireWindow");
                return;
            }

            // Добавляет planned action.
            DebugManager.DiagLog(
                $"[SuperJumpFromRoofOnRoof PLAN] ADD target={targetRoof.ObstacleType} " +
                $"targetIndex={targetRoofIndex} fireShift={fireShift:F3} " +
                $"energy={planningState.Hamster.Energy} projection={planningState.ProjectionWorldShift:F3}");
            actions.Add(BuildAction(_policy, planningState, targetRoof, targetRoofIndex, fireShift, travel));
        }

        private static void LogPlanReject(PlanningState planningState, string reason)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null)
                return;

            DebugManager.DiagLog(
                $"[SuperJumpFromRoofOnRoof PLAN] REJECT reason={reason} " +
                $"state={hamster.HamsterState} energy={hamster.Energy} " +
                $"isOnRoof={hamster.IsOnRoof} roofSupport={hamster.RoofSupportInstanceId.HasValue} " +
                $"isShifting={hamster.IsShifting} projection={planningState.ProjectionWorldShift:F3}");
        }

        /// <summary>
        /// Создает planned action для найденного fire shift.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpFromRoofOnRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot targetRoof,
            int targetRoofIndex,
            float fireShift,
            JumpFromRoofOnRoofTravel travel)
        {
            // Считает trigger position по target roof.
            float projectedTriggerX = targetRoof.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

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
                resultRoofSupportInstanceId: targetRoof.InstanceId);
        }
    }
}
