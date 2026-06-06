using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOnRoof
{
    /// <summary>
    /// Проверяет сохраненные jump-on-roof actions для role-based planning path.
    /// </summary>
    internal sealed class JumpOnRoofRetainedValidatorNew : IRetainedActionValidatorNew
    {
        private readonly IJumpOnRoofPolicy _policy;
        private readonly JumpOnRoofFireWindowFinderNew _fireWindowFinder;
        private readonly ObstacleChainBuilderNew _chainBuilder = new ObstacleChainBuilderNew();
        private readonly JumpOnRoofActionResolver _actionResolver = new JumpOnRoofActionResolver();

        /// <summary>
        /// Создает validator для сохраненных jump-on-roof actions.
        /// </summary>
        public JumpOnRoofRetainedValidatorNew(
            IJumpOnRoofPolicy policy,
            JumpOnRoofFireWindowFinderNew fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает true, если сохраненный action всё еще ведёт к той же крыше и безопасен.
        /// </summary>
        public bool IsStillValid(RetainedActionContextNew context)
        {
            if (context?.PlanningState?.Hamster == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || context.Action.TargetBottomLine.HasValue)
            {
                return false;
            }

            PlannedAction action = context.Action;
            HamsterSnapshot hamster = context.PlanningState.Hamster;
            if (!CanStillExecute(hamster, context.RetainedObstacle, action))
                return false;

            if (!_policy.TryGetTravel(out float jumpTravel))
                return false;

            if (!TryBuildCurrentLaneChain(context, out ObstacleChainNew sourceChain)
                || !_actionResolver.TryResolve(
                    sourceChain,
                    out ObstacleSnapshot resolvedRoof,
                    out int roofObstacleIndex,
                    out _))
            {
                return false;
            }

            if (resolvedRoof.InstanceId != context.RetainedObstacle.InstanceId)
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
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel,
                roofObstacleIndex);
        }

        /// <summary>
        /// Проверяет, может ли сохраненная крыша всё еще обрабатываться jump-on-roof.
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
                && ObstacleClassifier.IsObstacleWithRoof(targetObstacle.ObstacleType)
                && (!action.ResultRoofSupportInstanceId.HasValue
                    || action.ResultRoofSupportInstanceId.Value == targetObstacle.InstanceId);
        }

        /// <summary>
        /// Перестраивает role-based chain на текущей линии хомяка.
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
