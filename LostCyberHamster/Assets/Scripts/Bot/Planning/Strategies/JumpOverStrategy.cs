using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay.Enums;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    /// <summary>
    /// Строит и симулирует обычный прыжок через наземное препятствие.
    /// </summary>
    public sealed class JumpOverStrategy : IPlanningStrategy
    {
        private const string JumpClipName = "transform_jump";
        private const int JumpEnergyCost = 10;
        private const float LatestFireSafetyMargin = 0.03f;
        private const float EarliestClearanceMargin = 0.02f;

        private float? _jumpTravel;

        public BotActionKind ActionKind => BotActionKind.Jump;

        /// <summary>
        /// Добавляет кандидата прыжка для текущего blocking ground obstacle.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            if (actions == null)
                return;

            // В v1 стратегия работает только с обязательным наземным препятствием впереди.
            if (decisionPoint == null || decisionPoint.Kind != DecisionPointKind.BlockingGroundObstacle)
                return;

            ObstacleSnapshot targetObstacle = decisionPoint.Obstacle;
            if (!CanJumpOver(planningState, targetObstacle))
                return;

            if (!TryGetJumpTravel(out float jumpTravel))
                return;

            // Выбираем окно fire так, чтобы к концу jump-клипа obstacle уже осталось позади хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            float earliestFireShift =
                targetObstacle.RightX - hamster.HamsterLeftX - jumpTravel + EarliestClearanceMargin;
            float latestFireShift =
                targetObstacle.LeftX - hamster.HamsterRightX - LatestFireSafetyMargin;

            if (latestFireShift < 0f || earliestFireShift > latestFireShift)
                return;

            float fireShift = Mathf.Max(0f, earliestFireShift);
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            actions.Add(new PlannedAction(
                BotActionKind.Jump,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                targetObstacleIndex: decisionPoint.ObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: JumpEnergyCost,
                description: $"Jump over {targetObstacle.ObstacleType}"));
        }

        /// <summary>
        /// Моделирует успешный обычный jump-over и пересчитывает следующее planning-состояние.
        /// </summary>
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // После успешного jump-over хомяк остаётся на той же линии и возвращается в обычный Run.
            HamsterSnapshot hamster = planningState.Hamster;
            int energy = hamster.Energy - action.EnergyCost;
            if (energy < 0)
                energy = 0;

            var nextHamster = new HamsterSnapshot(
                HamsterStateEnum.Run,
                hamster.IsOnBottomLine,
                isOnRoof: false,
                energy,
                hamster.Lives,
                hamster.IsDamaged,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);

            // Сдвигаем прогноз мира на длительность завершённого прыжка и ищем следующий obstacle.
            float nextProjectionWorldShift = planningState.ProjectionWorldShift + action.CompletionWorldShift;
            int nextObstacleIndex = worldSnapshot.Obstacles.Count;
            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - nextProjectionWorldShift;
                if (projectedRightX > nextHamster.HamsterLeftX)
                {
                    nextObstacleIndex = obstacleIndex;
                    break;
                }
            }

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift);
        }

        private static bool CanJumpOver(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.Energy < JumpEnergyCost)
                return false;

            return ObstacleClassifier.CanJumpOverOnGround(targetObstacle.ObstacleType);
        }

        private bool TryGetJumpTravel(out float jumpTravel)
        {
            if (_jumpTravel.HasValue)
            {
                jumpTravel = _jumpTravel.Value;
                return true;
            }

            // Берём реальную длину runtime-клипа, чтобы planning и gameplay использовали один тайминг.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                jumpTravel = 0f;
                return false;
            }

            _jumpTravel = HelpMethods.GetWorldShiftForClip(controller, JumpClipName);
            jumpTravel = _jumpTravel.Value;
            return true;
        }
    }
}
