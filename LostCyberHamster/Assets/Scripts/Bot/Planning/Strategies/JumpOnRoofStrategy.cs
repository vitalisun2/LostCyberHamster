using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    /// <summary>
    /// Готовит planning-каркас для обычного прыжка с посадкой на препятствие с крышей.
    /// </summary>
    public sealed class JumpOnRoofStrategy : IPlanningStrategy
    {
        private const string _jumpClipName = "transform_jump";
        private const int _jumpEnergyCost = 10;

        private float? _jumpTravel;

        /// <summary>
        /// Тип действия для roof landing.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;

        /// <summary>
        /// Собирает кандидаты прыжка на крышу.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            // Работаем только с decision point для посадки на крышу.
            if (decisionPoint.Kind != DecisionPointKind.RoofLanding)
                return;

            // Берём target obstacle и его индекс для следующих расчётов fire window и planned action.
            ObstacleSnapshot targetObstacle = decisionPoint.Obstacle;
            Guard.NotNull((targetObstacle, "decisionPoint.Obstacle"));

            int targetObstacleIndex = decisionPoint.ObstacleIndex;

            // Отсекаем ситуации, где landing на крышу сейчас недоступен.
            if (!CanJumpOnRoof(planningState, targetObstacle))
                return;

            // Берём фактическую длину обычного jump-клипа для дальнейшего поиска fire window.
            if (!TryGetJumpTravel(out float jumpTravel))
                return;

            // Ищем момент запуска только там, где shared resolver подтверждает exact JumpOnRoof.
            if (!ActionWindowFinder.TryFindJumpOnRoofFireShift(
                    planningState,
                    worldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    jumpTravel,
                    out float fireShift))
            {
                return;
            }

            PlannedAction action = BuildJumpOnRoofAction(
                planningState,
                targetObstacle,
                targetObstacleIndex,
                fireShift,
                jumpTravel);

            actions.Add(action);
            DebugManager.DiagLog(
                $"[JumpOnRoof ACTION] ADD PlannedAction JumpOnRoof " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"fireShift={fireShift:0.###} triggerX={action.TriggerX:0.###}");
        }

        /// <summary>
        /// Симулирует переход в RoofRun.
        /// </summary>
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

            // Эта стратегия симулирует только своё jump-действие на крышу.
            if (action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterLanding(
                planningState.Hamster,
                action);

            return PlanningStateTransition.AdvanceAfterRoofLanding(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        /// <summary>
        /// Проверяет допустимость прыжка на крышу.
        /// </summary>
        private static bool CanJumpOnRoof(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.IsDamaged || hamster.Energy < _jumpEnergyCost)
                return false;

            return ObstacleClassifier.IsObstacleWithRoof(targetObstacle.ObstacleType);
        }

        /// <summary>
        /// Возвращает длину jump-клипа.
        /// </summary>
        private bool TryGetJumpTravel(out float jumpTravel)
        {
            if (_jumpTravel.HasValue)
            {
                jumpTravel = _jumpTravel.Value;
                return true;
            }

            // Берём реальную длину runtime-клипа, чтобы planning и gameplay использовали один тайминг.
            TransformAnimatorController controller = UnityEngine.Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                jumpTravel = 0f;
                return false;
            }

            _jumpTravel = HelpMethods.GetWorldShiftForClip(controller, _jumpClipName);
            jumpTravel = _jumpTravel.Value;
            return true;
        }

        /// <summary>
        /// Строит action для прыжка на крышу.
        /// </summary>
        private static PlannedAction BuildJumpOnRoofAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float jumpTravel)
        {
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.JumpOnRoof,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: _jumpEnergyCost,
                description: $"Jump on roof {targetObstacle.ObstacleType}");
        }
    }
}
