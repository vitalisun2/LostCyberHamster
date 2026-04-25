using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.Models
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
            float nextProjectionWorldShift = planningState.ProjectionWorldShift + action.CompletionWorldShift;
            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                planningState.NextObstacleIndex,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift);
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
            float nextProjectionWorldShift = planningState.ProjectionWorldShift + action.CompletionWorldShift;
            int minimumNextObstacleIndex = Math.Max(
                planningState.NextObstacleIndex,
                action.TargetObstacleIndex + 1);

            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                minimumNextObstacleIndex,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift);
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
                hamster.IsDamaged,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);
        }

        /// <summary>
        /// Возвращает состояние хомяка после успешной смены линии.
        /// </summary>
        public static HamsterSnapshot ApplyLaneSwitch(HamsterSnapshot hamster, PlannedAction action)
        {
            bool isOnRoof = action.TargetBottomLine.HasValue ? false : hamster.IsOnRoof;
            bool targetBottomLine = action.TargetBottomLine ?? hamster.IsOnBottomLine;
            int? roofSupportInstanceId = isOnRoof ? hamster.RoofSupportInstanceId : null;

            return new HamsterSnapshot(
                hamster.HamsterState,
                targetBottomLine,
                isOnRoof,
                hamster.Energy - action.EnergyCost,
                hamster.Lives,
                hamster.IsDamaged,
                isShifting: false,
                roofSupportInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);
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
                hamster.IsDamaged,
                isShifting: false,
                action.TargetObstacleInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);
        }

            /// <summary>
            /// Ищет следующий релевантный obstacle.
            /// </summary>
        private static int FindNextRelevantObstacleIndex(
            WorldSnapshot worldSnapshot,
            int startObstacleIndex,
            float projectionWorldShift,
            float hamsterLeftX)
        {
            for (int obstacleIndex = startObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - projectionWorldShift;
                if (projectedRightX > hamsterLeftX)
                    return obstacleIndex;
            }

            return worldSnapshot.Obstacles.Count;
        }
    }
}
