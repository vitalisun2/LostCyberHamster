using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof
{
    /// <summary>
    /// Проверяет сохраненные jump-from-roof actions для role-based planning path.
    /// </summary>
    internal sealed class JumpFromRoofRetainedValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly IJumpFromRoofPolicy _policy;
        private readonly JumpFromRoofFireWindowFinder _fireWindowFinder;
        private readonly JumpFromRoofSpecification _specification;
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();
        private readonly JumpFromRoofActionResolver _actionResolver = new JumpFromRoofActionResolver();

        /// <summary>
        /// Создает validator для сохраненных прыжков с крыши.
        /// </summary>
        public JumpFromRoofRetainedValidator(
            IJumpFromRoofPolicy policy,
            JumpFromRoofFireWindowFinder fireWindowFinder,
            JumpFromRoofSpecification specification)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
            _specification = specification;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает true, если сохраненный action всё ещё применим и безопасен.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет retained context.
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

            // Считывает актуальные runtime-дистанции и перестраивает текущую roof-exit chain.
            PlannedAction action = context.Action;
            if (!_policy.TryGetTravel(out JumpFromRoofTravel travel)
                || !TryBuildRoofExitChain(context, out ObstacleChain sourceChain))
            {
                return false;
            }

            // Проверяет, что текущая ситуация всё ещё ведёт к тому же blocking threat.
            if (!_actionResolver.TryResolve(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    sourceChain,
                    travel,
                    out ObstacleSnapshot resolvedThreat,
                    out _,
                    out ObstacleSnapshot lastRoof))
            {
                return false;
            }

            if (resolvedThreat.InstanceId != context.RetainedObstacle.InstanceId)
                return false;

            if (!_specification.IsSatisfiedBy(context.PlanningState, resolvedThreat))
                return false;

            // Пересчитывает fire-window и восстанавливает оставшийся fire shift.
            if (!JumpFromRoofChainCalculator.TryCalculate(
                    _policy,
                    context.PlanningState,
                    sourceChain,
                    lastRoof,
                    travel,
                    out JumpFromRoofChainModel chainModel))
            {
                return false;
            }

            if (!TryGetRemainingFireShift(
                    context.ProjectedWorldSnapshot,
                    context.RetainedObstacle,
                    action,
                    context.PlanningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            // Проверяет сохраненную точку запуска и runtime outcome.
            if (fireShift < chainModel.FirstFireShift - ValidationEpsilon
                || fireShift > chainModel.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(context.ProjectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                context.PlanningState,
                baseObstacles,
                fireShift,
                travel);
        }

        /// <summary>
        /// Перестраивает role-based chain с первого obstacle после passive roofs.
        /// </summary>
        private bool TryBuildRoofExitChain(
            RetainedActionContext context,
            out ObstacleChain chain)
        {
            int firstDetectionIndex = GetFirstDetectionIndex(
                context.PlanningState,
                context.ProjectedWorldSnapshot);

            return _chainBuilder.TryBuild(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                firstDetectionIndex,
                out chain);
        }

        /// <summary>
        /// Возвращает индекс, с которого roof-run detector начинает искать road situation.
        /// </summary>
        private static int GetFirstDetectionIndex(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot)
        {
            int defaultDetectionIndex = planningState.NextObstacleIndex;
            if (planningState?.Hamster == null || !planningState.Hamster.IsOnRoof)
                return defaultDetectionIndex;

            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out _,
                    out int lastRoofIndex))
            {
                return defaultDetectionIndex;
            }

            int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
            return firstIndexAfterPassiveRoofs > defaultDetectionIndex
                ? firstIndexAfterPassiveRoofs
                : defaultDetectionIndex;
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
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

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

            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
