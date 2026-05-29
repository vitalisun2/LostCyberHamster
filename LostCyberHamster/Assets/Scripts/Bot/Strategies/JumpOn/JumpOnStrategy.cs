using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.JumpOn
{
    /// <summary>
    /// Собирает компоненты обычного ground jump-on strategy.
    /// </summary>
    internal sealed class JumpOnStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Политика runtime-параметров обычного jump-on.
        /// </summary>
        private readonly IJumpOnPolicy _policy;

        /// <summary>
        /// Проверка применимости обычного jump-on к текущей decision chain.
        /// </summary>
        private readonly JumpOnSpecification _specification;

        /// <summary>
        /// Подбор безопасного момента запуска обычного jump-on.
        /// </summary>
        private readonly JumpOnFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Симулятор planning-состояния после обычного jump-on.
        /// </summary>
        private readonly JumpOnSimulator _simulator;

        public JumpOnStrategy()
        {
            // Инициализирует planning-компоненты стратегии.
            _policy = new JumpOnPolicy();
            _specification = new JumpOnSpecification(_policy);
            _fireWindowFinder = new JumpOnFireWindowFinder(_policy);
            _simulator = new JumpOnSimulator(_policy);

            // Инициализирует runtime-компоненты выполнения.
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOnExecutor(triggerGate);
            RetainedValidator = new JumpOnRetainedActionValidator(_policy, _fireWindowFinder);
            Simulator = _simulator;
        }

        /// <summary>
        /// Возвращает тип action, который строит стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime-исполнитель обычного jump-on.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Валидатор сохранённого обычного jump-on action.
        /// </summary>
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Симулятор planning-перехода для обычного jump-on.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет обычный jump-on action, если chain содержит валидный target и полное действие безопасно.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Подбирает action-chain: обычную decision chain или расширенную jump-on chain.
            if (!TryResolveActionChain(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    out ObstacleChain actionChain))
            {
                return;
            }

            // Получает runtime-дистанции действия.
            if (!_policy.TryGetTravel(out JumpOnTravel travel))
                return;

            // Подбирает подтвержденный момент запуска.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    actionChain,
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

            // Добавляет action в набор вариантов.
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
        /// Строит target-aware chain в пределах vision horizon и проверяет применимость обычного jump-on.
        /// </summary>
        private bool TryResolveActionChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out ObstacleChain actionChain)
        {
            // Строит chain с ближайшим target в допустимом horizon.
            if (!JumpOnTargetChainBuilder.TryBuildTargetChain(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    worldSnapshot.VisionRightEdgeX,
                    out actionChain))
            {
                return false;
            }

            // Проверяет применимость jump-on к найденной chain.
            return _specification.IsSatisfiedBy(
                planningState,
                actionChain,
                out _,
                out _);
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

            // Создаёт описание action.
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
                fulfillsJumpOnObjective: JumpOnObjectiveRules.HasEnergyForJumpOnObjective(planningState.Hamster));
        }
    }
}
