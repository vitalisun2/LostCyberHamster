using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    public sealed class SuperJumpOverStrategy : IPlanningStrategy
    {
        private const string _superJumpClipName = "transform_super_jump";
        private const int _superJumpEnergyCost = 20;

        private float? _superJumpTravel;

        public BotActionKind ActionKind => BotActionKind.SuperJump;

        /// <summary>
        /// Проверяет, можно ли перепрыгнуть текущий obstacle суперпрыжком, и добавляет действие с точкой запуска.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяем обязательный контекст стратегии.
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Работаем только с наземным блокером на текущей линии.
            if (decisionPoint.Kind != DecisionPointKind.BlockingGroundObstacle)
                return;

            // Берём target obstacle для расчёта момента запуска.
            ObstacleSnapshot targetObstacle = decisionPoint.Obstacle;
            Guard.NotNull((targetObstacle, "decisionPoint.Obstacle"));

            // Отсекаем ситуации, где super jump over невозможен.
            if (!CanSuperJumpOver(planningState, targetObstacle))
                return;

            // Берём фактическую длину runtime-клипа super jump.
            if (!TryGetSuperJumpTravel(out float superJumpTravel))
                return;

            // Ищем окно запуска, которое даст runtime-результат SuperJumpOver.
            if (!TryComputeSuperJumpOverFireShift(
                    planningState.Hamster,
                    targetObstacle,
                    superJumpTravel,
                    out float fireShift))
                return;

            // Добавляем действие, привязанное к live obstacle и рассчитанной точке запуска.
            float obstacleLeftXToFire = targetObstacle.LeftX - fireShift;
            float renderWorldX = obstacleLeftXToFire + planningState.ProjectionWorldShift;
            actions.Add(new PlannedAction(
                BotActionKind.SuperJump,
                obstacleLeftXToFire,
                renderWorldX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex: decisionPoint.ObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: _superJumpEnergyCost,
                description: $"Super jump over {targetObstacle.ObstacleType}"));
        }

        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяем обязательный контекст симуляции.
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (action, nameof(action)),
                (worldSnapshot, nameof(worldSnapshot)));

            // Эта стратегия симулирует только своё действие.
            if (action.Kind != ActionKind)
                return null;

            // После успешного super jump over хомяк остаётся на той же линии и возвращается в обычный Run.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        private static bool CanSuperJumpOver(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.IsDamaged || hamster.Energy < _superJumpEnergyCost)
                return false;

            return ObstacleClassifier.CanSuperJumpOverOnGround(targetObstacle.ObstacleType);
        }

        private static bool TryComputeSuperJumpOverFireShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            float superJumpTravel,
            out float fireShift)
        {
            // Первый валидный старт: obstacle успеет оказаться позади к концу клипа.
            float firstValidFireShift =
                targetObstacle.RightX - hamster.HamsterLeftX - superJumpTravel;

            // Последний валидный старт: obstacle ещё впереди хомяка на момент fire.
            float lastValidFireShift =
                targetObstacle.LeftX - hamster.HamsterRightX;

            if (lastValidFireShift < 0f || firstValidFireShift > lastValidFireShift)
            {
                fireShift = 0f;
                return false;
            }

            // Выбираем самый ранний доступный запуск внутри окна.
            fireShift = Mathf.Max(0f, firstValidFireShift);
            return true;
        }

        private bool TryGetSuperJumpTravel(out float superJumpTravel)
        {
            // Возвращаем кеш.
            if (_superJumpTravel.HasValue)
            {
                superJumpTravel = _superJumpTravel.Value;
                return true;
            }

            // Ищем runtime-контроллер.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            Guard.NotNull((controller, nameof(TransformAnimatorController)));

            // Кешируем длину клипа.
            _superJumpTravel = HelpMethods.GetWorldShiftForClip(controller, _superJumpClipName);
            superJumpTravel = _superJumpTravel.Value;
            return true;
        }
    }
}
