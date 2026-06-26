using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.Simulation
{
    /// <summary>
    /// Содержит чистые переходы planning-состояния после успешного действия бота.
    /// </summary>
    internal static class PlanningStateTransition
    {
        /// <summary>
        /// Возвращает planning-состояние после завершения действия и сдвига мира.
        /// </summary>
        public static PlanningState Advance(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster)
        {
            float nextProjectionWorldShift = GetNextProjectionWorldShift(planningState, action);
            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex: 0,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                planningState.RemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                planningState.RemovedObstacleInstanceIds);
        }

        /// <summary>
        /// Возвращает planning-состояние после смены линии, пересчитывая ближайший obstacle для новой линии с начала snapshot.
        /// </summary>
        public static PlanningState AdvanceAfterLaneSwitch(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster)
        {
            float nextProjectionWorldShift = GetNextProjectionWorldShift(planningState, action);
            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex: 0,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                planningState.RemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                planningState.RemovedObstacleInstanceIds);
        }

        /// <summary>
        /// Возвращает planning-состояние после посадки на крышу target obstacle.
        /// </summary>
        public static PlanningState AdvanceAfterRoofLanding(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster)
        {
            float nextProjectionWorldShift = GetNextProjectionWorldShift(planningState, action);
            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex: 0,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                planningState.RemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                planningState.RemovedObstacleInstanceIds);
        }

        /// <summary>
        /// Возвращает planning-состояние после действия, которое удаляет target obstacle.
        /// </summary>
        public static PlanningState AdvanceAfterTargetRemoval(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster)
        {
            float nextProjectionWorldShift = GetNextProjectionWorldShift(planningState, action);
            IReadOnlyList<int> nextRemovedObstacleInstanceIds =
                planningState.GetRemovedObstacleInstanceIdsWith(action.TargetObstacleInstanceId);

            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex: 0,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                nextRemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                nextRemovedObstacleInstanceIds);
        }

        /// <summary>
        /// Возвращает planning-состояние после passive pickup collectable.
        /// </summary>
        public static PlanningState AdvanceAfterCollectiblePickup(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster)
        {
            float nextProjectionWorldShift = GetNextProjectionWorldShift(planningState, action);
            IReadOnlyList<int> nextRemovedObstacleInstanceIds =
                planningState.GetRemovedObstacleInstanceIdsWith(action.TargetObstacleInstanceId);

            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex: 0,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                nextRemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                nextRemovedObstacleInstanceIds);
        }

        /// <summary>
        /// Возвращает planning-состояние после roof jump over над препятствием на текущей крыше.
        /// </summary>
        public static PlanningState AdvanceAfterRoofJumpOver(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster)
        {
            float nextProjectionWorldShift = GetNextProjectionWorldShift(planningState, action);
            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex: 0,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                planningState.RemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                planningState.RemovedObstacleInstanceIds);
        }

        /// <summary>
        /// Возвращает состояние хомяка после успешного over-действия с возвратом в Run.
        /// </summary>
        public static HamsterSnapshot ApplyRunAfterOver(HamsterSnapshot hamster, PlannedAction action)
        {
            return new HamsterSnapshot(
                HamsterStateEnum.Run,
                hamster.IsOnBottomLine,
                isOnRoof: false,
                hamster.Energy - action.EnergyCost,
                hamster.Lives,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }

        /// <summary>
        /// Возвращает состояние хомяка после успешной смены линии.
        /// </summary>
        public static HamsterSnapshot ApplyLaneSwitch(HamsterSnapshot hamster, PlannedAction action)
        {
            // Определяет, остается ли хомяк на roof support после смены линии.
            bool keepsRoofSupport = hamster.IsOnRoof && action.ResultRoofSupportInstanceId.HasValue;
            bool isOnRoof = keepsRoofSupport || (!action.TargetBottomLine.HasValue && hamster.IsOnRoof);
            bool targetBottomLine = action.TargetBottomLine ?? hamster.IsOnBottomLine;
            int? roofSupportInstanceId = keepsRoofSupport
                ? action.ResultRoofSupportInstanceId
                : (isOnRoof ? hamster.RoofSupportInstanceId : null);
            HamsterStateEnum hamsterState = hamster.HamsterState == HamsterStateEnum.RoofRun && !isOnRoof
                ? HamsterStateEnum.RunFromRoof
                : hamster.HamsterState;

            // Возвращает состояние после завершения смены линии.
            return new HamsterSnapshot(
                hamsterState,
                targetBottomLine,
                isOnRoof,
                hamster.Energy - action.EnergyCost,
                hamster.Lives,
                isShifting: false,
                roofSupportInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }

        /// <summary>
        /// Возвращает состояние хомяка после успешной посадки на крышу.
        /// </summary>
        public static HamsterSnapshot ApplyRoofRunAfterLanding(HamsterSnapshot hamster, PlannedAction action)
        {
            return new HamsterSnapshot(
                HamsterStateEnum.RoofRun,
                hamster.IsOnBottomLine,
                isOnRoof: true,
                hamster.Energy - action.EnergyCost,
                hamster.Lives,
                isShifting: false,
                action.TargetObstacleInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }

        /// <summary>
        /// Возвращает состояние хомяка после успешного roof jump over с продолжением RoofRun.
        /// </summary>
        public static HamsterSnapshot ApplyRoofRunAfterRoofJumpOver(HamsterSnapshot hamster, PlannedAction action)
        {
            int? roofSupportInstanceId = action.ResultRoofSupportInstanceId ?? hamster.RoofSupportInstanceId;

            return new HamsterSnapshot(
                HamsterStateEnum.RoofRun,
                hamster.IsOnBottomLine,
                isOnRoof: true,
                hamster.Energy - action.EnergyCost,
                hamster.Lives,
                isShifting: false,
                roofSupportInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }

        /// <summary>
        /// Ищет следующий релевантный obstacle.
        /// </summary>
        private static int FindNextRelevantObstacleIndex(
            WorldSnapshot worldSnapshot,
            int startObstacleIndex,
            float projectionWorldShift,
            float hamsterLeftX,
            IReadOnlyList<int> removedObstacleInstanceIds)
        {
            for (int obstacleIndex = startObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (IsObstacleRemoved(obstacle.InstanceId, removedObstacleInstanceIds))
                    continue;

                float projectedRightX = obstacle.RightX - projectionWorldShift;
                if (projectedRightX > hamsterLeftX)
                    return obstacleIndex;
            }

            return worldSnapshot.Obstacles.Count;
        }

        /// <summary>
        /// Возвращает projection shift после action и guard-участка безопасного Run re-entry.
        /// </summary>
        private static float GetNextProjectionWorldShift(
            PlanningState planningState,
            PlannedAction action)
        {
            return planningState.ProjectionWorldShift
                + action.CompletionWorldShift
                + JumpPlanningConstants.PostActionReentryGuardTravel;
        }

        private static bool IsObstacleRemoved(
            int obstacleInstanceId,
            IReadOnlyList<int> removedObstacleInstanceIds)
        {
            if (removedObstacleInstanceIds == null)
                return false;

            for (int index = 0; index < removedObstacleInstanceIds.Count; index++)
            {
                if (removedObstacleInstanceIds[index] == obstacleInstanceId)
                    return true;
            }

            return false;
        }
    }
}
