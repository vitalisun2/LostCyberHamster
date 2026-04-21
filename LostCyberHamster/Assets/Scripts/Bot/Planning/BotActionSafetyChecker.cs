using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Central pre-simulation safety gate for planned bot actions.
    /// </summary>
    public sealed class BotActionSafetyChecker
    {
        public bool IsSafe(PlanningState state, PlannedAction action, WorldSnapshot world)
        {
            if (state == null || action == null || world == null)
                return false;

            return action.Kind switch
            {
                BotActionKind.Jump => IsSafeJumpOver(state, action, world),
                BotActionKind.SuperJump => IsSafeSuperJumpOver(state, action, world),
                BotActionKind.Tap => IsSafeSwitchLane(state, action),
                _ => false
            };
        }

        private static bool IsSafeJumpOver(PlanningState state, PlannedAction action, WorldSnapshot world)
        {
            HamsterSnapshot hamster = state.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting)
                return false;

            if (hamster.IsDamaged)
                return true;

            WorldSnapshot projectedWorld = PlanningSnapshotProjector.Project(world, state);
            if (projectedWorld == null)
                return false;

            float fireShift = action.CompletionWorldShift - action.PostFireWorldShift;
            if (fireShift < 0f)
                return false;

            float jumpShift = action.PostFireWorldShift;
            for (int obstacleIndex = 0; obstacleIndex < projectedWorld.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = ShiftObstacle(projectedWorld.Obstacles[obstacleIndex], fireShift);
                if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (obstacle.CenterX <= HamsterCenterX(hamster))
                    continue;

                if (IsBeyondReachRight(hamster, jumpShift, obstacle))
                    break;

                if (WouldJumpDamage(hamster, jumpShift, obstacle, projectedWorld.Obstacles, fireShift))
                    return false;
            }

            return true;
        }

        private static bool IsSafeSuperJumpOver(PlanningState state, PlannedAction action, WorldSnapshot world)
        {
            return true;
        }

        private static bool IsSafeSwitchLane(PlanningState state, PlannedAction action)
        {
            return action.TargetBottomLine.HasValue
                && action.TargetBottomLine.Value != state.IsOnBottomLine;
        }

        private static bool WouldJumpDamage(
            HamsterSnapshot hamster,
            float jumpShift,
            ObstacleSnapshot obstacle,
            System.Collections.Generic.IReadOnlyList<ObstacleSnapshot> projectedObstacles,
            float fireShift)
        {
            switch (obstacle.ObstacleType)
            {
                case ObstacleTypeEnum.smallAlive:
                    return !IsHamsterCenterInsideObstacleAtShift(hamster, jumpShift, obstacle, HamsterWidth(hamster) * 0.2f)
                           && IsOverlapAtShift(hamster, jumpShift, obstacle);
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return IsOverlapAtShift(hamster, jumpShift, obstacle);
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return WouldSmallNotAliveRoadAndRoofDamage(
                        hamster,
                        jumpShift,
                        obstacle,
                        projectedObstacles,
                        fireShift);
                case ObstacleTypeEnum.bigAlive:
                    return IsOverlapAtShift(hamster, jumpShift, obstacle) || IsWithinReach(hamster, jumpShift, obstacle);
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return IsOverlapAtShift(hamster, jumpShift, obstacle)
                           && IsHitSmallNotAliveOnRoof(hamster, jumpShift, projectedObstacles, fireShift);
                default:
                    return false;
            }
        }

        private static bool WouldSmallNotAliveRoadAndRoofDamage(
            HamsterSnapshot hamster,
            float jumpShift,
            ObstacleSnapshot small,
            System.Collections.Generic.IReadOnlyList<ObstacleSnapshot> projectedObstacles,
            float fireShift)
        {
            if (IsJumpOver(hamster, jumpShift, small))
                return false;

            if (!IsOverlapAtShift(hamster, jumpShift, small))
                return false;

            if (TryFindRoofUnderSmall(small, projectedObstacles, fireShift, out ObstacleSnapshot roof))
                return IsOverlapAtShift(hamster, jumpShift, roof)
                       && IsHitSmallNotAliveOnRoof(hamster, jumpShift, projectedObstacles, fireShift);

            return true;
        }

        private static bool IsHitSmallNotAliveOnRoof(
            HamsterSnapshot hamster,
            float worldShift,
            System.Collections.Generic.IReadOnlyList<ObstacleSnapshot> projectedObstacles,
            float fireShift)
        {
            for (int obstacleIndex = 0; obstacleIndex < projectedObstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = ShiftObstacle(projectedObstacles[obstacleIndex], fireShift);
                if (obstacle.ObstacleType != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                if (!TryFindRoofUnderSmall(obstacle, projectedObstacles, fireShift, out _))
                    continue;

                if (IsOverlapAtShift(hamster, worldShift, obstacle))
                    return true;
            }

            return false;
        }

        private static bool TryFindRoofUnderSmall(
            ObstacleSnapshot small,
            System.Collections.Generic.IReadOnlyList<ObstacleSnapshot> projectedObstacles,
            float fireShift,
            out ObstacleSnapshot roof)
        {
            for (int obstacleIndex = 0; obstacleIndex < projectedObstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot candidate = ShiftObstacle(projectedObstacles[obstacleIndex], fireShift);
                if (candidate.IsBottomLine != small.IsBottomLine)
                    continue;

                if (!CollisionUtils.IsRoofObstacle(candidate.ObstacleType))
                    continue;

                if (IsOverlap(small.LeftX, small.RightX, candidate.LeftX, candidate.RightX))
                {
                    roof = candidate;
                    return true;
                }
            }

            roof = null;
            return false;
        }

        private static ObstacleSnapshot ShiftObstacle(ObstacleSnapshot obstacle, float worldShift)
        {
            return new ObstacleSnapshot(
                obstacle.InstanceId,
                obstacle.ObstacleType,
                obstacle.IsTopLine,
                obstacle.LeftX - worldShift,
                obstacle.RightX - worldShift,
                obstacle.CenterX - worldShift);
        }

        private static bool IsBeyondReachRight(HamsterSnapshot hamster, float reachShift, ObstacleSnapshot obstacle)
        {
            return obstacle.LeftX - reachShift > hamster.HamsterRightX + 0.0001f;
        }

        private static bool IsWithinReach(HamsterSnapshot hamster, float reachShift, ObstacleSnapshot obstacle)
        {
            return obstacle.LeftX - reachShift <= hamster.HamsterRightX + 0.0001f;
        }

        private static bool IsOverlapAtShift(HamsterSnapshot hamster, float worldShift, ObstacleSnapshot obstacle)
        {
            return IsOverlap(
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                obstacle.LeftX - worldShift,
                obstacle.RightX - worldShift);
        }

        private static bool IsJumpOver(HamsterSnapshot hamster, float worldShift, ObstacleSnapshot obstacle)
        {
            float obstacleEndLeft = obstacle.LeftX - worldShift;
            float obstacleEndRight = obstacle.RightX - worldShift;
            bool clearStart = hamster.HamsterRightX < obstacle.LeftX;
            bool clearEnd = hamster.HamsterLeftX > obstacleEndRight;
            bool noOverlap = !IsOverlap(hamster.HamsterLeftX, hamster.HamsterRightX, obstacleEndLeft, obstacleEndRight);
            return clearStart && clearEnd && noOverlap;
        }

        private static bool IsHamsterCenterInsideObstacleAtShift(
            HamsterSnapshot hamster,
            float worldShift,
            ObstacleSnapshot obstacle,
            float rightTolerance)
        {
            float left = obstacle.LeftX - worldShift;
            float right = obstacle.RightX - worldShift + rightTolerance;
            return HamsterCenterX(hamster) >= left && HamsterCenterX(hamster) <= right;
        }

        private static bool IsOverlap(float leftA, float rightA, float leftB, float rightB)
        {
            return rightA > leftB && rightB > leftA;
        }

        private static float HamsterCenterX(HamsterSnapshot hamster)
        {
            return (hamster.HamsterLeftX + hamster.HamsterRightX) * 0.5f;
        }

        private static float HamsterWidth(HamsterSnapshot hamster)
        {
            return hamster.HamsterRightX - hamster.HamsterLeftX;
        }
    }
}
