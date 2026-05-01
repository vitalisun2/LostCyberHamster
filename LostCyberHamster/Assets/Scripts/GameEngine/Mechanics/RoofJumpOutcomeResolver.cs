using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public static class RoofJumpOutcomeResolver
    {
        private const float RightEdgeToleranceRatio = 0.2f;
        private const int NoTarget = -1;

        public static JumpResolveResult ResolveRoofJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            JumpResolveResult noHit = new(HamsterStateEnum.JumpFromRoof, NoTarget);

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != context.IsBottomLine)
                    continue;

                if (obstacle.CenterX <= context.HamsterCenterX)
                    continue;

                if (ShouldBreakByReachRight(context, obstacle))
                    break;

                JumpResolveResult result = HandleObstacle(obstacle, obstacleIndex, obstacles, context, noHit);
                if (result.State != noHit.State)
                    return result;
            }

            return noHit;
        }

        private static JumpResolveResult HandleObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            switch (obstacle.Type)
            {
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return HandleRoofObstacle(obstacle, obstacleIndex, obstacles, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return HandleSmallNotAliveRoadAndRoof(obstacle, obstacleIndex, obstacles, context, noHit);
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallAlive:
                    return HandleLiveObstacle(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift)
                        ? new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex)
                        : noHit;
                default:
                    return noHit;
            }
        }

        private static JumpResolveResult HandleRoofObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (!IsOverlapAtShift(context, obstacle, context.RoofJumpShift))
                return noHit;

            bool hitSmall = JumpOutcomeResolver.TryFindDamagingRoofOccupantOnRoof(
                    obstacles,
                    obstacleIndex,
                    out int roofHazardIndex)
                && IsOverlapAtShift(context, obstacles[roofHazardIndex], context.RoofJumpShift);

            return new JumpResolveResult(
                hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump,
                obstacleIndex);
        }

        private static JumpResolveResult HandleSmallNotAliveRoadAndRoof(
            JumpObstacleData small,
            int smallIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (JumpOutcomeResolver.TryFindRoofUnderSmall(small, obstacles, out int roofIndex))
            {
                bool hitSmall = JumpOutcomeResolver.TryFindDamagingRoofOccupantOnRoof(
                        obstacles,
                        roofIndex,
                        out int roofHazardIndex)
                    && IsOverlapAtShift(context, obstacles[roofHazardIndex], context.RoofJumpShift);

                return new JumpResolveResult(
                    hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump,
                    roofIndex);
            }

            return IsOverlapAtShift(context, small, context.JumpFromRoofShift)
                ? new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, smallIndex)
                : noHit;
        }

        private static JumpResolveResult HandleLiveObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            float rightTolerance = context.HamsterWidth * RightEdgeToleranceRatio;
            if (IsHamsterCenterInsideObstacleAtShift(
                    context,
                    obstacle,
                    context.JumpFromRoofShift,
                    rightTolerance))
            {
                return new JumpResolveResult(HamsterStateEnum.JumpOnObstacleFromRoof, obstacleIndex);
            }

            return IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift)
                ? new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex)
                : noHit;
        }

        private static bool ShouldBreakByReachRight(RoofJumpResolveContext context, JumpObstacleData obstacle)
        {
            return obstacle.LeftX - context.ReachShift > context.HamsterRightX + 0.0001f;
        }

        private static bool IsOverlapAtShift(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle,
            float shift)
        {
            return CollisionUtils.IsOverlap(
                context.HamsterLeftX,
                context.HamsterRightX,
                obstacle.LeftX - shift,
                obstacle.RightX - shift);
        }

        private static bool IsHamsterCenterInsideObstacleAtShift(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle,
            float shift,
            float rightTolerance)
        {
            float left = obstacle.LeftX - shift;
            float right = obstacle.RightX - shift + rightTolerance;
            return context.HamsterCenterX >= left && context.HamsterCenterX <= right;
        }
    }
}
