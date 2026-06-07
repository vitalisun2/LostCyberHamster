using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Проверяет сохраненные roof-to-road jump-on actions для role-based planning path.
    /// </summary>
    internal sealed class JumpOnFromRoofRetainedValidatorNew : IRetainedActionValidatorNew
    {
        /// <summary>
        /// Policy конкретного варианта roof-to-road jump-on.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        /// <summary>
        /// Finder для повторной проверки fire-window и runtime outcome.
        /// </summary>
        private readonly JumpOnFromRoofFireWindowFinderNew _fireWindowFinder;

        /// <summary>
        /// Specification применимости сохраненного action к target.
        /// </summary>
        private readonly JumpOnFromRoofSpecificationNew _specification;

        /// <summary>
        /// Detector для восстановления актуальной role-based ситуации.
        /// </summary>
        private readonly DecisionPointDetectorNew _decisionPointDetector = new DecisionPointDetectorNew();

        /// <summary>
        /// Resolver target и roof context внутри актуальной role-based ситуации.
        /// </summary>
        private readonly JumpOnFromRoofActionResolver _actionResolver = new JumpOnFromRoofActionResolver();

        public JumpOnFromRoofRetainedValidatorNew(
            IJumpOnFromRoofPolicy policy,
            JumpOnFromRoofFireWindowFinderNew fireWindowFinder,
            JumpOnFromRoofSpecificationNew specification)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
            _specification = specification;
        }

        /// <summary>
        /// Тип action, который проверяет validator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает true, если сохраненный roof-to-road jump-on action все еще актуален и безопасен.
        /// </summary>
        public bool IsStillValid(RetainedActionContextNew context)
        {
            // Проверяет retained context и action contract.
            if (context?.PlanningState?.Hamster == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || context.Action.TargetBottomLine.HasValue
                || context.Action.ResultRoofSupportInstanceId.HasValue)
            {
                return false;
            }

            // Получает runtime-дистанции и актуальную role-based ситуацию.
            PlannedAction action = context.Action;
            if (!_policy.TryGetTravel(out JumpOnFromRoofTravel travel)
                || !_decisionPointDetector.TryDetect(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    out DecisionPointNew decisionPoint))
            {
                return false;
            }

            // Повторно выбирает target и roof context.
            if (!_actionResolver.TryResolve(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out _,
                    out ObstacleSnapshot resolvedTarget,
                    out int targetObstacleIndex,
                    out _,
                    out _))
            {
                return false;
            }

            // Сверяет сохраненный target с актуальным.
            if (resolvedTarget.InstanceId != context.RetainedObstacle.InstanceId)
                return false;

            if (action.TargetObstacleInstanceId != resolvedTarget.InstanceId)
                return false;

            if (!_specification.IsSatisfiedBy(context.PlanningState, resolvedTarget))
                return false;

            // Восстанавливает текущий fire shift.
            if (!TryGetRemainingFireShift(
                    context.ProjectedWorldSnapshot,
                    context.RetainedObstacle,
                    action,
                    context.PlanningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            if (fireShift < 0f)
                fireShift = 0f;

            // Подтверждает runtime outcome и post-action safety.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(context.ProjectedWorldSnapshot);
            if (!_fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                    context.PlanningState.Hamster,
                    baseObstacles,
                    fireShift,
                    travel,
                    targetObstacleIndex,
                    resolvedTarget.InstanceId))
            {
                return false;
            }

            return TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                targetObstacleIndex,
                resolvedTarget.InstanceId,
                fireShift + travel.ActionTravel);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift сохраненного action по trigger obstacle.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            // Отсекает неполные данные.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Ищет live trigger obstacle.
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

            // Использует target как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
