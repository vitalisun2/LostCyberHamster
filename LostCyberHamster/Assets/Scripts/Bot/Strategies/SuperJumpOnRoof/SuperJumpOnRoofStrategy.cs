using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Строит действия бота для super jump на крышу препятствия.
    /// </summary>
    internal sealed class SuperJumpOnRoofStrategy : IPlanningStrategy
    {
        private readonly IJumpOnRoofPolicy _policy;
        private readonly JumpOnRoofSpecification _specification;
        private readonly JumpOnRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpOnRoofSimulator _simulator;

        public SuperJumpOnRoofStrategy()
        {
            // Инициализирует зависимости стратегии.
            _policy = new SuperJumpOnRoofPolicy();
            _specification = new JumpOnRoofSpecification(_policy);
            _fireWindowFinder = new JumpOnRoofFireWindowFinder(_policy);
            _simulator = new JumpOnRoofSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует обработчики и симулятор наружу.
            Executor = new SuperJumpOnRoofExecutor(triggerGate);
            RetainedValidator = new JumpOnRoofRetainedActionValidator(_policy, _fireWindowFinder);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет в план действие super jump на крышу, если для текущей точки решения найдены все условия выполнения.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет обязательные входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Проверяет состояние хомяка перед поиском roof target.
            if (!_specification.IsSatisfiedBy(planningState))
                return;

            // Получает фактическую дальность super jump из runtime-анимации и upgrade-delay.
            if (!_policy.TryGetTravel(out float superJumpTravel))
                return;

            // Ищет допустимый момент срабатывания действия.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    superJumpTravel,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out float fireShift))
            {
                return;
            }

            // Добавляет готовое действие в результирующий список.
            ObstacleSnapshot triggerObstacle = decisionPoint.Chain.FirstObstacle;
            actions.Add(BuildAction(_policy, planningState, triggerObstacle, targetObstacle, targetObstacleIndex, fireShift, superJumpTravel));
        }

        /// <summary>
        /// Создаёт спланированное действие super jump на крышу с рассчитанными координатами и метаданными цели.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOnRoofPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float superJumpTravel)
        {
            // Оставляет trigger в абсолютной runtime-линии перед хомяком.
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;

            // Формирует итоговое плановое действие.
            return new PlannedAction(
                policy.ActionKind,
                triggerX: projectedTriggerX,
                renderWorldX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {targetObstacle.ObstacleType}");
        }
    }
}
