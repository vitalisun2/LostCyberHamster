using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoof
{
    /// <summary>
    /// Собирает role-based кандидаты super-прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class SuperJumpFromRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpFromRoofPolicy _policy;
        private readonly JumpFromRoofSpecification _specification;
        private readonly JumpFromRoofActionResolver _actionResolver;
        private readonly JumpFromRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpFromRoofSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты super-прыжка с крыши.
        /// </summary>
        public SuperJumpFromRoofStrategy()
        {
            _policy = new SuperJumpFromRoofPolicy();
            _specification = new JumpFromRoofSpecification(_policy);
            _actionResolver = new JumpFromRoofActionResolver();
            _fireWindowFinder = new JumpFromRoofFireWindowFinder(_policy);
            _simulator = new JumpFromRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpFromRoofExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new JumpFromRoofRetainedValidator(
                _policy,
                _fireWindowFinder,
                _specification);
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Добавляет super-jump-from-roof action, если passive roof exit опасен, а super-прыжок безопасен.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет вход и получает runtime travel.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            if (!_policy.TryGetTravel(out JumpFromRoofTravel travel))
                return;

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
                return;
            }

            if (!_specification.IsSatisfiedBy(planningState, blockingThreat))
                return;

            // Подтверждает fire-window через runtime resolver.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    lastRoof,
                    travel,
                    out JumpFromRoofChainModel chainModel,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(_policy, planningState, chainModel, fireShift, travel));
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
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                chainModel.FirstFireShift,
                chainModel.LastFireShift);

            // Возвращает safe action без локального сравнения с ordinary-вариантом.
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
