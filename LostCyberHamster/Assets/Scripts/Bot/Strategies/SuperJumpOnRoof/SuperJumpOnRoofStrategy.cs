using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Строит действия бота для super jump на крышу препятствия.
    /// </summary>
    internal sealed class SuperJumpOnRoofStrategy : IPlanningStrategy
    {
        private const string _superJumpClipName = "transform_super_jump";

        private readonly SuperJumpOnRoofSpecification _specification;
        private readonly SuperJumpOnRoofFireWindowFinder _fireWindowFinder;
        private readonly SuperJumpOnRoofSimulator _simulator;

        public SuperJumpOnRoofStrategy()
        {
            // Инициализирует зависимости стратегии.
            _specification = new SuperJumpOnRoofSpecification();
            _fireWindowFinder = new SuperJumpOnRoofFireWindowFinder();
            _simulator = new SuperJumpOnRoofSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует обработчики и симулятор наружу.
            Executor = new SuperJumpOnRoofExecutor(triggerGate);
            RetainedValidator = new SuperJumpOnRoofRetainedActionValidator();
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SuperJumpOnRoof;
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
            if (!TryGetSuperJumpTravel(out float superJumpTravel))
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
            actions.Add(BuildAction(planningState, triggerObstacle, targetObstacle, targetObstacleIndex, fireShift, superJumpTravel));
        }

        /// <summary>
        /// Создаёт спланированное действие super jump на крышу с рассчитанными координатами и метаданными цели.
        /// </summary>
        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float superJumpTravel)
        {
            // Оставляет trigger в абсолютной runtime-линии перед хомяком.
            float triggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            // Формирует итоговое плановое действие.
            return new PlannedAction(
                BotActionKind.SuperJumpOnRoof,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                triggerObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: SuperJumpOnRoofSpecification.EnergyCost,
                description: $"Super jump on roof {targetObstacle.ObstacleType}");
        }

        /// <summary>
        /// Возвращает runtime-дистанцию super jump с учётом задержки upgrade-запроса.
        /// </summary>
        private static bool TryGetSuperJumpTravel(out float superJumpTravel)
        {
            // Находит контроллер анимаций в активной сцене.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                superJumpTravel = 0f;
                return false;
            }

            // Складывает дистанцию super jump clip и путь мира за половину double-jump окна.
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            float upgradeDelayTravel = halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
            superJumpTravel = HelpMethods.GetWorldShiftForClip(controller, _superJumpClipName) + upgradeDelayTravel;
            return true;
        }
    }
}