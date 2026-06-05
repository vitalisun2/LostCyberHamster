using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOn
{
    /// <summary>
    /// Validates retained JumpOn actions for the role-based planning path.
    /// </summary>
    internal sealed class JumpOnRetainedValidatorNew : IRetainedActionValidatorNew
    {
        private readonly IJumpOnPolicy _policy;
        private readonly JumpOnFireWindowFinderNew _fireWindowFinder;
        private readonly ObstacleChainBuilderNew _chainBuilder = new ObstacleChainBuilderNew();

        /// <summary>
        /// Creates a role-based validator for retained JumpOn actions.
        /// </summary>
        public JumpOnRetainedValidatorNew(
            IJumpOnPolicy policy,
            JumpOnFireWindowFinderNew fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Checks whether the retained JumpOn action is still relevant and safe.
        /// </summary>
        public bool IsStillValid(RetainedActionContextNew context)
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

            if (!TryBuildCurrentLaneChain(context, out ObstacleChainNew chain)
                || !TryFindRetainedTargetInChain(
                    chain,
                    context.RetainedObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex))
            {
                return false;
            }

            if (!_policy.TryGetTravel(out JumpOnTravel travel))
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
        /// Checks whether the retained target can still be handled by ground JumpOn.
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
        /// Rebuilds the role-based chain on the hamster's current lane.
        /// </summary>
        private bool TryBuildCurrentLaneChain(
            RetainedActionContextNew context,
            out ObstacleChainNew chain)
        {
            return _chainBuilder.TryBuild(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                context.PlanningState.NextObstacleIndex,
                out chain);
        }

        /// <summary>
        /// Finds the retained target inside the current role-based chain.
        /// </summary>
        private static bool TryFindRetainedTargetInChain(
            ObstacleChainNew chain,
            ObstacleSnapshot retainedTarget,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex)
        {
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            if (chain == null || retainedTarget == null)
                return false;

            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElementNew element = chain.Elements[chainIndex];
                if (element.Obstacle.InstanceId != retainedTarget.InstanceId)
                    continue;

                if (!element.HasRole(ObstacleRole.Target)
                    || !ObstacleClassifier.CanJumpOnGroundObstacle(element.Obstacle.ObstacleType))
                {
                    return false;
                }

                targetObstacleIndex = element.WorldIndex;
                targetObstacleChainIndex = chainIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Restores the remaining fire shift for the retained action by trigger obstacle.
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
