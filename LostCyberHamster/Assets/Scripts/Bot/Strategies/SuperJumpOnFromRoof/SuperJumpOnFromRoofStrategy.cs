using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnFromRoof
{
    /// <summary>
    /// Собирает super roof-to-road jump-on actions.
    /// </summary>
    internal sealed class SuperJumpOnFromRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Политика runtime-параметров super roof-to-road jump-on.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        /// <summary>
        /// Проверка применимости super roof-to-road jump-on к найденной road target-chain.
        /// </summary>
        private readonly JumpOnFromRoofSpecification _specification;

        /// <summary>
        /// Подбор безопасного момента запуска super roof-to-road jump-on.
        /// </summary>
        private readonly JumpOnFromRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Симулятор planning-состояния после super roof-to-road jump-on.
        /// </summary>
        private readonly JumpOnFromRoofSimulator _simulator;

        public SuperJumpOnFromRoofStrategy()
        {
            // Инициализирует planning-компоненты стратегии.
            _policy = new SuperJumpOnFromRoofPolicy();
            _specification = new JumpOnFromRoofSpecification(_policy);
            _fireWindowFinder = new JumpOnFromRoofFireWindowFinder(_policy);
            _simulator = new JumpOnFromRoofSimulator(_policy);

            // Инициализирует runtime-компоненты выполнения.
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOnFromRoofExecutor(triggerGate);
            RetainedValidator = new JumpOnFromRoofRetainedActionValidator(_policy, _fireWindowFinder, _specification);
            Simulator = _simulator;
        }

        /// <summary>
        /// Возвращает тип action, который строит стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime-исполнитель super roof-to-road jump-on.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Валидатор сохранённого super roof-to-road jump-on action.
        /// </summary>
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Симулятор planning-перехода для super roof-to-road jump-on.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет super roof-to-road jump-on action, если target и полное действие безопасны.
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

            // Получает runtime-дистанции действия.
            if (!_policy.TryGetTravel(out JumpOnFromRoofTravel travel))
                return;

            // Подбирает road target-chain после конца passive roof path.
            if (!TryResolveActionChain(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    travel,
                    out ObstacleChain actionChain,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex,
                    out ObstacleSnapshot lastRoof))
            {
                return;
            }

            // Подбирает подтвержденный момент запуска.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    actionChain,
                    travel,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    lastRoof,
                    out JumpOnFromRoofWindowModel window,
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
        /// Берет road target-chain из decision point и проверяет применимость action.
        /// </summary>
        private bool TryResolveActionChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            JumpOnFromRoofTravel travel,
            out ObstacleChain actionChain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex,
            out ObstacleSnapshot lastRoof)
        {
            // Инициализирует пустой результат.
            actionChain = null;
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            lastRoof = null;

            // Проверяет, что detector уже нашел roof-exit target-chain.
            if (decisionPoint?.Kind != DecisionPointKind.JumpOnFromRoofTarget
                || decisionPoint.Chain == null)
            {
                return false;
            }

            // Восстанавливает последнюю passive roof для расчета окна схода.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out lastRoof,
                    out _))
            {
                return false;
            }

            // Проверяет roof jump-on правила для найденной road-chain.
            actionChain = decisionPoint.Chain;
            return _specification.IsSatisfiedBy(
                planningState,
                actionChain,
                lastRoof,
                travel,
                out targetObstacle,
                out targetObstacleIndex,
                out targetObstacleChainIndex);
        }

        /// <summary>
        /// Создаёт planning action для super roof-to-road jump-on.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOnFromRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            JumpOnFromRoofWindowModel window,
            float fireShift,
            JumpOnFromRoofTravel travel,
            float completionWorldShift)
        {
            // Вычисляет координату запуска.
            ObstacleSnapshot targetObstacle = window.TargetObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;

            // Создаёт описание action.
            return new PlannedAction(
                policy.ActionKind,
                triggerX: projectedTriggerX,
                renderWorldX,
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
