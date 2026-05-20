using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный прыжок с крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofRetainedActionValidator : IRetainedActionValidator
    {
        /// <summary>
        /// Допуск для проверки сохраненного fire shift относительно границ окна.
        /// </summary>
        private const float ValidationEpsilon = 0.0001f;

        private readonly IJumpFromRoofOnRoofPolicy _policy;
        private readonly JumpFromRoofOnRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpFromRoofOnRoofSpecification _specification;

        public JumpFromRoofOnRoofRetainedActionValidator(
            IJumpFromRoofOnRoofPolicy policy,
            JumpFromRoofOnRoofFireWindowFinder fireWindowFinder,
            JumpFromRoofOnRoofSpecification specification)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
            _specification = specification;
        }

        /// <summary>
        /// Тип действия, который проверяет validator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, остается ли сохраненный action применимым к текущему planning context.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет action и kind.
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            DecisionPoint decisionPoint = context.DecisionPoint;
            ObstacleSnapshot retainedTargetRoof = context.TargetObstacle;
            PlannedAction action = context.Action;

            // Проверяет обязательные данные.
            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || retainedTargetRoof == null)
            {
                return false;
            }

            // Повторяет specification-gate, чтобы retained action не обходил semantic applicability.
            if (!_specification.IsSatisfiedBy(planningState))
                return false;

            // Считывает актуальные runtime-дистанции.
            if (!_policy.TryGetTravel(out JumpFromRoofOnRoofTravel travel))
                return false;

            // Заново находит target roof для текущего roof-to-roof сценария.
            if (!_fireWindowFinder.TryFindTargetRoof(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out ObstacleSnapshot lastRoof,
                    out ObstacleSnapshot runFromRoofBlocker,
                    out ObstacleSnapshot lastObstacleBeforeTargetRoof,
                    out ObstacleSnapshot targetRoof,
                    out int targetRoofIndex))
            {
                return false;
            }

            if (targetRoof.InstanceId != retainedTargetRoof.InstanceId)
                return false;

            if (action.TargetObstacleInstanceId != targetRoof.InstanceId)
                return false;

            if (action.TargetObstacleIndex != targetRoofIndex)
                return false;

            if (action.ResultRoofSupportInstanceId != targetRoof.InstanceId)
                return false;

            // Пересчитывает актуальное fire window.
            if (!JumpFromRoofOnRoofWindowCalculator.TryCalculate(
                    planningState,
                    lastRoof,
                    targetRoof,
                    runFromRoofBlocker,
                    lastObstacleBeforeTargetRoof,
                    _policy.BigAliveCollisionPaddingRatio,
                    travel,
                    out float firstFireShift,
                    out float lastFireShift,
                    out _))
            {
                return false;
            }

            // Восстанавливает текущий fire shift.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetRoof,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            // Проверяет попадание fire shift в окно.
            if (fireShift < firstFireShift - ValidationEpsilon
                || fireShift > lastFireShift + ValidationEpsilon)
            {
                return false;
            }

            // Подтверждает outcome через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                targetRoof.InstanceId,
                fireShift,
                travel);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift сохраненного action по target roof.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetRoof,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            // Проверяет вход.
            if (projectedWorldSnapshot == null || targetRoof == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Ищет execution anchor.
            float projectedTriggerX = action.TriggerX - projectionWorldShift;
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - projectedTriggerX;
                    return true;
                }
            }

            // Использует target roof как fallback.
            fireShift = targetRoof.LeftX - projectedTriggerX;
            return true;
        }
    }
}
