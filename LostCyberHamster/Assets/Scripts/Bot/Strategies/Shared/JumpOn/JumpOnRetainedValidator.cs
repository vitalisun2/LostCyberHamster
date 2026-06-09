using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Проверяет сохраненные JumpOn actions для role-based planning path.
    /// </summary>
    internal sealed class JumpOnRetainedValidator : IRetainedActionValidator
    {
        private readonly IJumpOnPolicy _policy;
        private readonly JumpOnFireWindowFinder _fireWindowFinder;
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();
        private readonly JumpOnActionChainResolver _actionChainResolver = new JumpOnActionChainResolver();

        /// <summary>
        /// Создает role-based validator для сохраненных JumpOn actions.
        /// </summary>
        public JumpOnRetainedValidator(
            IJumpOnPolicy policy,
            JumpOnFireWindowFinder fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает true, если сохраненный JumpOn action все еще актуален и безопасен.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
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

            PlannedAction action = context.Action;
            HamsterSnapshot hamster = context.PlanningState.Hamster;
            if (!CanStillExecute(hamster, context.RetainedObstacle, action))
                return false;

            if (!_policy.TryGetTravel(out JumpOnTravel travel))
                return false;

            if (!TryBuildCurrentLaneChain(context, out ObstacleChain sourceChain)
                || !_actionChainResolver.TryResolve(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    sourceChain,
                    travel,
                    out _,
                    out ObstacleSnapshot resolvedTarget,
                    out int targetObstacleIndex,
                    out _))
            {
                return false;
            }

            if (resolvedTarget.InstanceId != context.RetainedObstacle.InstanceId)
                return false;

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

            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(context.ProjectedWorldSnapshot);
            if (!_fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                    hamster,
                    baseObstacles,
                    fireShift,
                    travel,
                    targetObstacleIndex))
            {
                return false;
            }

            return TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                targetObstacleIndex,
                context.RetainedObstacle.InstanceId,
                fireShift + travel.ActionTravel);
        }

        /// <summary>
        /// Проверяет, может ли сохраненный target все еще обрабатываться ground JumpOn.
        /// </summary>
        private bool CanStillExecute(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            PlannedAction action)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.Run
                && !hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.Energy >= action.EnergyCost
                && targetObstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.CanJumpOnGroundObstacle(targetObstacle.ObstacleType);
        }

        /// <summary>
        /// Перестраивает role-based chain на текущей линии хомяка.
        /// </summary>
        private bool TryBuildCurrentLaneChain(
            RetainedActionContext context,
            out ObstacleChain chain)
        {
            return _chainBuilder.TryBuild(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                context.PlanningState.NextObstacleIndex,
                out chain);
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
