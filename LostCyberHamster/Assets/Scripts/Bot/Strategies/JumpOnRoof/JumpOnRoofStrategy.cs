using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Строит действия бота для прыжка на крышу препятствия.
    /// </summary>
    internal sealed class JumpOnRoofStrategy : IPlanningStrategy
    {
        private const string _jumpClipName = "transform_jump";

        private readonly JumpOnRoofSpecification _specification;
        private readonly JumpOnRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpOnRoofSimulator _simulator;

        public JumpOnRoofStrategy()
        {
            // Инициализирует зависимости стратегии.
            _specification = new JumpOnRoofSpecification();
            _fireWindowFinder = new JumpOnRoofFireWindowFinder();
            _simulator = new JumpOnRoofSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует обработчики и симулятор наружу.
            Executor = new JumpOnRoofExecutor(triggerGate);
            RetainedValidator = new JumpOnRoofRetainedActionValidator();
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет в план действие прыжка на крышу, если для текущей точки решения найдены все условия выполнения.
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

            // Получает фактическую дальность прыжка из runtime-анимации.
            if (!TryGetJumpTravel(out float jumpTravel))
                return;

            // Ищет допустимый момент срабатывания действия.
            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    jumpTravel,
                    out ObstacleSnapshot targetObstacle,
                    out int targetObstacleIndex,
                    out float fireShift))
            {
                return;
            }

            // Добавляет готовое действие в результирующий список.
            ObstacleSnapshot triggerObstacle = decisionPoint.Chain.FirstObstacle;
            actions.Add(BuildAction(planningState, triggerObstacle, targetObstacle, targetObstacleIndex, fireShift, jumpTravel));
        }

        /// <summary>
        /// Создаёт спланированное действие прыжка на крышу с рассчитанными координатами и метаданными цели.
        /// </summary>
        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float jumpTravel)
        {
            // Оставляет trigger в абсолютной runtime-линии перед хомяком.
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;

            // Формирует итоговое плановое действие.
            return new PlannedAction(
                BotActionKind.JumpOnRoof,
                triggerX: projectedTriggerX,
                renderWorldX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: JumpOnRoofSpecification.EnergyCost,
                description: $"Jump on roof {targetObstacle.ObstacleType}");
        }

        /// <summary>
        /// Возвращает runtime-дистанцию обычного jump animation clip.
        /// </summary>
        private static bool TryGetJumpTravel(out float jumpTravel)
        {
            // Находит контроллер анимаций в активной сцене.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                jumpTravel = 0f;
                return false;
            }

            // Считывает world shift для клипа прыжка.
            jumpTravel = HelpMethods.GetWorldShiftForClip(controller, _jumpClipName);
            return true;
        }
    }
}
