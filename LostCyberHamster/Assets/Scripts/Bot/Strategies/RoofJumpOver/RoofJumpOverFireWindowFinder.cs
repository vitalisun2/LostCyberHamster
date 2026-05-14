using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Ищет fire shift для roof jump over над small obstacle на крыше.
    /// </summary>
    internal sealed class RoofJumpOverFireWindowFinder
    {
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot hazardObstacle,
            ObstacleSnapshot supportObstacle,
            float roofJumpOverTravel,
            float jumpFromRoofTravel,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (hazardObstacle, nameof(hazardObstacle)),
                (supportObstacle, nameof(supportObstacle)));

            if (!TryGetOpenWindow(
                    planningState.Hamster,
                    hazardObstacle,
                    roofJumpOverTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                fireShift = 0f;
                return false;
            }

            fireShift = (firstFireShift + lastFireShift) * 0.5f;

            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                supportObstacle.InstanceId,
                fireShift,
                roofJumpOverTravel,
                jumpFromRoofTravel);
        }

        internal static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            ObstacleSnapshot hazardObstacle,
            float roofJumpOverTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            firstFireShift = 0f;
            lastFireShift = 0f;

            if (hamster == null || hazardObstacle == null)
                return false;

            firstFireShift = hazardObstacle.RightX - hamster.HamsterLeftX - roofJumpOverTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = hazardObstacle.LeftX - hamster.HamsterRightX;

            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return firstFireShift < lastFireShift;
        }

        internal static bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int expectedSupportInstanceId,
            float fireShift,
            float roofJumpOverTravel,
            float jumpFromRoofTravel)
        {
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                roofJumpOverTravel,
                jumpFromRoofTravel);

            JumpResolveResult result = RoofJumpOutcomeResolver.ResolveRoofJump(obstaclesAtFireShift, context);
            if (result.State != HamsterStateEnum.RoofJump)
                return false;

            if (result.TargetIndex < 0 || result.TargetIndex >= obstaclesAtFireShift.Count)
                return false;

            return obstaclesAtFireShift[result.TargetIndex].InstanceId == expectedSupportInstanceId;
        }
    }
}
