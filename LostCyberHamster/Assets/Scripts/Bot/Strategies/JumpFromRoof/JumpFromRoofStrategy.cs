using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpFromRoof
{
    /// <summary>
    /// Собирает действия обычного прыжка с крыши через дорожные obstacles.
    /// </summary>
    internal sealed class JumpFromRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Хранит runtime-отличия обычного прыжка с крыши.
        /// </summary>
        private readonly IJumpFromRoofPolicy _policy;

        /// <summary>
        /// Проверяет применимость strategy к decision point.
        /// </summary>
        private readonly JumpFromRoofSpecification _specification;

        /// <summary>
        /// Подбирает fire shift и подтверждает его runtime resolver-ом.
        /// </summary>
        private readonly JumpFromRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Симулирует planning-переход после действия.
        /// </summary>
        private readonly JumpFromRoofSimulator _simulator;

        public JumpFromRoofStrategy()
        {
            _policy = new JumpFromRoofPolicy();
            _specification = new JumpFromRoofSpecification(_policy);
            _fireWindowFinder = new JumpFromRoofFireWindowFinder(_policy);
            _simulator = new JumpFromRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpFromRoofExecutor(triggerGate);
            RetainedValidator = new JumpFromRoofRetainedActionValidator(_policy, _fireWindowFinder, _specification);
            Simulator = _simulator;
        }

        /// <summary>
        /// Тип действия, которое строит strategy.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor для действия.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Validator для сохраненного action.
        /// </summary>
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Simulator planning-переходов для действия.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет возможный прыжок с крыши в список planned actions.
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

            // Получает runtime-дистанции.
            if (!_policy.TryGetTravel(out JumpFromRoofTravel travel))
                return;

            // Проверяет применимость strategy.
            if (!_specification.IsSatisfiedBy(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    travel,
                    out _,
                    out _,
                    out ObstacleSnapshot lastRoof))
            {
                return;
            }

            // Подбирает fire shift.
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

            // Добавляет planned action.
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
            // Считает trigger position.
            ObstacleSnapshot targetObstacle = chainModel.FirstObstacle;
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            // Возвращает action.
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
                description: BuildDescription(policy, chainModel));
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
