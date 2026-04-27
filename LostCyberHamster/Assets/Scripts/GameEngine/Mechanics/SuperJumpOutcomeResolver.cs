using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using System.Collections.Generic;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public static class SuperJumpOutcomeResolver
    {
        private const float _rightEdgeToleranceRatio = 0.2f;
        private const int _noTarget = -1;

        public static JumpResolveResult ResolveSuperJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            JumpResolveResult noHit = new(HamsterStateEnum.SuperJump, _noTarget);
            JumpResolveResult overResult = noHit;

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
                if (result.State == HamsterStateEnum.SuperJumpOver)
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
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return HandleRoofObstacle(obstacle, obstacleIndex, obstacles, context, noHit);
                case ObstacleTypeEnum.bigAlive:
                    return HandleBigAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallAlive:
                    return HandleSmallAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return HandleSmallNotAliveRoad(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return HandleSmallNotAliveRoadAndRoof(obstacle, obstacleIndex, obstacles, context, noHit);
                default:
                    return noHit;
            }
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

            bool hitSmall = JumpOutcomeResolver.TryFindDamagingRoofOccupantOnRoof(obstacles, obstacleIndex, out int roofHazardIndex)
                && IsOverlapAtShift(context, obstacles[roofHazardIndex]);
            HamsterStateEnum state = hitSmall
                ? HamsterStateEnum.SuperJumpOnRoofDamage
                : HamsterStateEnum.SuperJumpOnRoof;
            return new JumpResolveResult(state, obstacleIndex);
        }

        private static JumpResolveResult HandleBigAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (IsOverlapAtShift(context, obstacle))
                return new JumpResolveResult(HamsterStateEnum.SuperJumpDamage, obstacleIndex);

            return IsJumpOver(context, obstacle)
                ? new JumpResolveResult(HamsterStateEnum.SuperJumpOver, obstacleIndex)
                : noHit;
        }

        private static JumpResolveResult HandleSmallAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            float rightTolerance = context.HamsterWidth * _rightEdgeToleranceRatio;
            if (IsHamsterCenterInsideObstacleAtShift(context, obstacle, rightTolerance))
                return new JumpResolveResult(HamsterStateEnum.SuperJumpOnObstacle, obstacleIndex);

            if (IsOverlapAtShift(context, obstacle))
                return new JumpResolveResult(HamsterStateEnum.SuperJumpDamage, obstacleIndex);

            return IsJumpOver(context, obstacle)
                ? new JumpResolveResult(HamsterStateEnum.SuperJumpOver, obstacleIndex)
                : noHit;
        }

        private static JumpResolveResult HandleSmallNotAliveRoad(
            JumpObstacleData obstacle,
            int obstacleIndex,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (IsOverlapAtShift(context, obstacle))
                return new JumpResolveResult(HamsterStateEnum.SuperJumpDamage, obstacleIndex);

            return IsJumpOver(context, obstacle)
                ? new JumpResolveResult(HamsterStateEnum.SuperJumpOver, obstacleIndex)
                : noHit;
        }

        private static JumpResolveResult HandleSmallNotAliveRoadAndRoof(
            JumpObstacleData small,
            int smallIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context,
            JumpResolveResult noHit)
        {
            bool isOverlapSmall = IsOverlapAtShift(context, small);
            if (!isOverlapSmall)
            {
                bool isJumpOverSmall = IsJumpOver(context, small);
                if (!isJumpOverSmall)
                    return noHit;
            }

            if (TryFindRoofUnderSmall(small, obstacles, out int roofIndex)
                && IsOverlapAtShift(context, obstacles[roofIndex]))
            {
                bool hitSmallOnRoof = JumpOutcomeResolver.TryFindDamagingRoofOccupantOnRoof(obstacles, roofIndex, out int roofHazardIndex)
                    && IsOverlapAtShift(context, obstacles[roofHazardIndex]);
                HamsterStateEnum state = hitSmallOnRoof
                    ? HamsterStateEnum.SuperJumpOnRoofDamage
                    : HamsterStateEnum.SuperJumpOnRoof;
                return new JumpResolveResult(state, roofIndex);
            }

            return isOverlapSmall
                ? new JumpResolveResult(HamsterStateEnum.SuperJumpDamage, smallIndex)
                : new JumpResolveResult(HamsterStateEnum.SuperJumpOver, smallIndex);
        }

        private static bool TryFindRoofUnderSmall(
            JumpObstacleData small,
            IReadOnlyList<JumpObstacleData> obstacles,
            out int roofIndex)
        {
            return JumpOutcomeResolver.TryFindRoofUnderSmall(small, obstacles, out roofIndex);
        }

        private static bool ShouldBreakByReachRight(JumpResolveContext context, JumpObstacleData obstacle)
        {
            return obstacle.LeftX - context.ReachShift > context.HamsterRightX + 0.0001f;
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