using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Строит общую planning-модель пассивного схода с крыши.
    /// </summary>
    internal static class PassiveRoofExitPlanner
    {
        /// <summary>
        /// Возвращает модель passive roof exit, если её можно безопасно рассмотреть в текущем context.
        /// </summary>
        public static bool TryBuildModel(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            float runFromRoofTravel,
            out PassiveRoofExitModel model)
        {
            model = default;

            if (planningState == null || worldSnapshot == null || runFromRoofTravel <= 0f)
                return Fail(out model);

            HamsterSnapshot hamster = planningState.Hamster;
            if (!CanExitRoofPassively(hamster))
                return Fail(out model);

            if (decisionPoint == null || decisionPoint.Chain == null || !decisionPoint.IsDecisionRequired)
                return Fail(out model);

            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out _))
            {
                return Fail(out model);
            }

            ObstacleSnapshot contextObstacle = decisionPoint.Obstacle;
            if (contextObstacle == null || contextObstacle.RightX <= hamster.HamsterLeftX)
                return Fail(out model);

            float exitStartShift = CalculateExitStartShift(hamster, lastRoof);
            float completionWorldShift = exitStartShift + runFromRoofTravel;
            if (!IsSafeDuringRunFromRoof(hamster, worldSnapshot, exitStartShift, completionWorldShift))
                return Fail(out model);

            model = new PassiveRoofExitModel(
                lastRoof,
                contextObstacle,
                decisionPoint.ObstacleIndex,
                exitStartShift,
                completionWorldShift);

            return true;
        }

        private static bool Fail(out PassiveRoofExitModel model)
        {
            model = default;
            return false;
        }

        private static bool CanExitRoofPassively(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.RoofSupportInstanceId.HasValue;
        }

        private static float CalculateExitStartShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot lastRoof)
        {
            float exitStartX = lastRoof.RightX + hamster.Width * RoofRunProjection.PassiveContinuationGapFactor;
            return Math.Max(0f, exitStartX - hamster.HamsterRightX);
        }

        private static bool IsSafeDuringRunFromRoof(
            HamsterSnapshot hamster,
            WorldSnapshot worldSnapshot,
            float exitStartShift,
            float completionWorldShift)
        {
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (OverlapsHamsterDuringShift(hamster, obstacle, exitStartShift, completionWorldShift))
                    return false;
            }

            return true;
        }

        private static bool OverlapsHamsterDuringShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            float startShift,
            float endShift)
        {
            float firstOverlapShift = obstacle.LeftX - hamster.HamsterRightX;
            float lastOverlapShift = obstacle.RightX - hamster.HamsterLeftX;

            return firstOverlapShift <= endShift
                && lastOverlapShift >= startShift;
        }
    }
}
