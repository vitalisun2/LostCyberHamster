using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using System.Collections.Generic;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public static class JumpOutcomeResolver
    {
        private const float RightEdgeToleranceRatio = 0.2f;
        private const int NoTarget = -1;

        public static JumpResolveResult ResolveJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            JumpResolveResult noHit = new(HamsterStateEnum.Jump, NoTarget);
            JumpResolveResult overResult = noHit;

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (obstacle.IsBottomLine != context.IsBottomLine)
                    continue;

                if (obstacle.CenterX <= context.HamsterCenterX)
                    continue;

                if (ShouldBreakByReachRight(context, obstacle))
                    break;

                JumpResolveResult result = HandleObstacle(obstacle, obstacleIndex, obstacles, context, noHit);
                if (result.State == HamsterStateEnum.JumpOver)
                {
                    overResult = result;
                    continue;
                }

                if (result.State != noHit.State)
                    return result;
            }

            return overResult;
        }

        private static JumpResolveResult HandleObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            switch (obstacle.Type)
            {
                case ObstacleTypeEnum.smallAlive:
                    return HandleSmallAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return HandleSmallNotAliveRoad(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return HandleSmallNotAliveRoadAndRoof(obstacle, obstacleIndex, obstacles, context, noHit);
                case ObstacleTypeEnum.bigAlive:
                    return IsHitBigAlive(obstacle, context)
                        ? new JumpResolveResult(HamsterStateEnum.JumpDamageForBigAlive, obstacleIndex)
                        : noHit;
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return HandleRoofObstacle(obstacle, obstacleIndex, obstacles, context, noHit);
                default:
                    return noHit;
            }
        }

        private static JumpResolveResult HandleSmallAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            float rightTolerance = context.HamsterWidth * RightEdgeToleranceRatio;
            if (IsHamsterCenterInsideObstacleAtShift(context, obstacle, rightTolerance))
                return new JumpResolveResult(HamsterStateEnum.JumpOnObstacle, obstacleIndex);

            if (IsOverlapAtShift(context, obstacle))
                return new JumpResolveResult(HamsterStateEnum.JumpDamageForSmallAlive, obstacleIndex);

            return IsJumpOver(context, obstacle)
                ? new JumpResolveResult(HamsterStateEnum.JumpOver, obstacleIndex)
                : noHit;
        }

        private static JumpResolveResult HandleSmallNotAliveRoad(
            JumpObstacleData obstacle,
            int obstacleIndex,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (IsOverlapAtShift(context, obstacle))
                return new JumpResolveResult(HamsterStateEnum.JumpDamageForSmallNotAlive, obstacleIndex);

            return IsJumpOver(context, obstacle)
                ? new JumpResolveResult(HamsterStateEnum.JumpOver, obstacleIndex)
                : noHit;
        }

        private static JumpResolveResult HandleSmallNotAliveRoadAndRoof(
            JumpObstacleData small,
            int smallIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (IsJumpOver(context, small))
                return new JumpResolveResult(HamsterStateEnum.JumpOver, smallIndex);

            if (!IsOverlapAtShift(context, small))
                return noHit;

            if (TryFindRoofUnderSmall(small, obstacles, out int roofIndex))
            {
                bool hitSmall = TryFindDamagingRoofOccupantOnRoof(obstacles, roofIndex, out int roofHazardIndex)
                    && IsOverlapAtShift(context, obstacles[roofHazardIndex]);
                HamsterStateEnum state = hitSmall
                    ? HamsterStateEnum.JumpOnRoofDamage
                    : HamsterStateEnum.JumpOnRoof;
                return new JumpResolveResult(state, roofIndex);
            }

            return new JumpResolveResult(HamsterStateEnum.JumpDamageForSmallNotAlive, smallIndex);
        }

        private static JumpResolveResult HandleRoofObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (!IsOverlapAtShift(context, obstacle))
                return noHit;

            bool hitSmall = TryFindDamagingRoofOccupantOnRoof(obstacles, obstacleIndex, out int roofHazardIndex)
                && IsOverlapAtShift(context, obstacles[roofHazardIndex]);
            HamsterStateEnum state = hitSmall
                ? HamsterStateEnum.JumpOnRoofDamage
                : HamsterStateEnum.JumpOnRoof;
            return new JumpResolveResult(state, obstacleIndex);
        }

        private static bool IsHitBigAlive(JumpObstacleData obstacle, JumpResolveContext context)
        {
            bool hitX = IsOverlapAtShift(context, obstacle);
            if (context.HasJumpMidY && obstacle.HasY)
                return hitX || CollisionUtils.IsOverlap(
                    context.HamsterJumpMidBottomY,
                    context.HamsterJumpMidTopY,
                    obstacle.BottomY,
                    obstacle.TopY);

            return hitX || (context.DamageBigAliveWithoutYByReach && IsWithinReach(context, obstacle));
        }

        internal static bool TryFindRoofUnderSmall(
            JumpObstacleData small,
            IReadOnlyList<JumpObstacleData> obstacles,
            out int roofIndex)
        {
            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData candidate = obstacles[obstacleIndex];
                if (candidate.IsRemovedInPlanning)
                    continue;

                if (candidate.IsBottomLine != small.IsBottomLine)
                    continue;

                if (!CollisionUtils.IsRoofObstacle(candidate.Type))
                    continue;

                if (CollisionUtils.IsOverlap(small.LeftX, small.RightX, candidate.LeftX, candidate.RightX))
                {
                    roofIndex = obstacleIndex;
                    return true;
                }
            }

            roofIndex = NoTarget;
            return false;
        }

        internal static bool TryFindDamagingRoofOccupantOnRoof(
            IReadOnlyList<JumpObstacleData> obstacles,
            int roofIndex,
            out int occupantIndex)
        {
            if (obstacles == null || roofIndex < 0 || roofIndex >= obstacles.Count)
            {
                occupantIndex = NoTarget;
                return false;
            }

            JumpObstacleData roofObstacle = obstacles[roofIndex];
            if (roofObstacle.IsRemovedInPlanning)
            {
                occupantIndex = NoTarget;
                return false;
            }

            if (!CollisionUtils.IsRoofObstacle(roofObstacle.Type))
            {
                occupantIndex = NoTarget;
                return false;
            }

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData candidate = obstacles[obstacleIndex];
                if (candidate.IsRemovedInPlanning)
                    continue;

                if (candidate.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                if (candidate.IsBottomLine != roofObstacle.IsBottomLine)
                    continue;

                if (!TryFindRoofUnderSmall(candidate, obstacles, out int supportRoofIndex))
                    continue;

                if (supportRoofIndex != roofIndex)
                    continue;

                occupantIndex = obstacleIndex;
                return true;
            }

            occupantIndex = NoTarget;
            return false;
        }

        private static bool ShouldBreakByReachRight(JumpResolveContext context, JumpObstacleData obstacle)
        {
            return obstacle.LeftX - context.ReachShift > context.HamsterRightX + 0.0001f;
        }

        private static bool IsWithinReach(JumpResolveContext context, JumpObstacleData obstacle)
        {
            return obstacle.LeftX - context.ReachShift <= context.HamsterRightX + 0.0001f;
        }

        private static bool IsOverlapAtShift(JumpResolveContext context, JumpObstacleData obstacle)
        {
            return CollisionUtils.IsOverlap(
                context.HamsterLeftX,
                context.HamsterRightX,
                obstacle.LeftX - context.JumpShift,
                obstacle.RightX - context.JumpShift);
        }

        private static bool IsJumpOver(JumpResolveContext context, JumpObstacleData obstacle)
        {
            float obstacleEndLeft = obstacle.LeftX - context.JumpShift;
            float obstacleEndRight = obstacle.RightX - context.JumpShift;
            return CollisionUtils.IsJumpOverIntervals(
                context.HamsterLeftX,
                context.HamsterRightX,
                obstacle.LeftX,
                obstacle.RightX,
                obstacleEndLeft,
                obstacleEndRight,
                0f);
        }

        private static bool IsHamsterCenterInsideObstacleAtShift(
            JumpResolveContext context,
            JumpObstacleData obstacle,
            float rightTolerance)
        {
            float left = obstacle.LeftX - context.JumpShift;
            float right = obstacle.RightX - context.JumpShift + rightTolerance;
            return context.HamsterCenterX >= left && context.HamsterCenterX <= right;
        }
    }
}
