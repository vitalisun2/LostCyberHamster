using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpFromRoof
{
    /// <summary>
    /// Собирает role-based кандидаты обычного прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class JumpFromRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpFromRoofPolicy _policy;
        private readonly JumpFromRoofSpecification _specification;
        private readonly JumpFromRoofActionResolver _actionResolver;
        private readonly JumpFromRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpFromRoofSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты обычного прыжка с крыши.
        /// </summary>
        public JumpFromRoofStrategy()
        {
            _policy = new JumpFromRoofPolicy();
            _specification = new JumpFromRoofSpecification();
            _actionResolver = new JumpFromRoofActionResolver();
            _fireWindowFinder = new JumpFromRoofFireWindowFinder(_policy);
            _simulator = new JumpFromRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpFromRoofExecutor(triggerGate);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет roof-run ситуацию с дорожной угрозой для прыжка с крыши.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.IsRoofRunCurrentLane(planningState, decisionPoint)
                && PlanningStrategyApplicability.FirstHasRole(decisionPoint, ObstacleRole.BlockingThreat);
        }

        /// <summary>
        /// Добавляет jump-from-roof action, если passive roof exit опасен, а прыжок безопасен.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            // Проверяет вход и получает runtime travel.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            if (!_policy.TryGetTravel(out JumpFromRoofTravel travel))
                return PlanningStrategyResult.NotApplicable();

            // Выбирает road threat, для которой passive exit опасен.
            if (!_actionResolver.TryResolve(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out ObstacleSnapshot blockingThreat,
                    out _,
                    out ObstacleSnapshot lastRoof))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            if (!_specification.IsSubjectValid(planningState, blockingThreat))
                return PlanningStrategyResult.NotApplicable();

            // Проверяет ресурс применимой strategy до поиска safe-window.
            if (planningState.Hamster.Energy < _policy.EnergyCost)
            {
                return PlanningStrategyResult.InsufficientEnergy(
                    nameof(JumpFromRoofStrategy),
                    _policy.ActionKind,
                    _policy.EnergyCost,
                    planningState.Hamster.Energy);
            }

            // Подтверждает fire-window через runtime resolver.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    lastRoof,
                    travel,
                    out JumpFromRoofChainModel chainModel,
                    out float fireShift,
                    out string deadEndReason))
            {
                return DeadEnd(deadEndReason);
            }

            return PlanningStrategyResult.FromAction(BuildAction(_policy, planningState, chainModel, fireShift, travel));
        }

        /// <summary>
        /// Создает dead-end результат для применимой jump-from-roof strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(JumpFromRoofStrategy), message);
        }

        /// <summary>
        /// Создает planned action для найденного fire shift.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpFromRoofPolicy policy,
            PlanningState planningState,
            JumpFromRoofChainModel chainModel,
            float fireShift,
            JumpFromRoofTravel travel)
        {
            // Считает trigger position относительно первой threat.
            ObstacleSnapshot targetObstacle = chainModel.FirstObstacle;
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                chainModel.FirstFireShift,
                chainModel.LastFireShift);

            // Возвращает safe action без локального сравнения с super-вариантом.
            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + travel.ActionTravel,
                postFireWorldShift: travel.ActionTravel,
                chainModel.LastObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: BuildDescription(policy, chainModel),
                triggerWindow: triggerWindow);
        }

        /// <summary>
        /// Формирует описание planned action.
        /// </summary>
        private static string BuildDescription(
            IJumpFromRoofPolicy policy,
            JumpFromRoofChainModel chainModel)
        {
            string baseDescription = $"{policy.DescriptionPrefix} {chainModel.FirstObstacle.ObstacleType}";
            return chainModel.ObstacleCount <= 1
                ? baseDescription
                : $"{baseDescription} x{chainModel.ObstacleCount}";
        }
    }
}
