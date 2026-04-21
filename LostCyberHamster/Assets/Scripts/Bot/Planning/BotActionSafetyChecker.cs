using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using System.Collections.Generic;

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
            List<JumpObstacleData> obstacles = new(projectedWorld.Obstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < projectedWorld.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorld.Obstacles[obstacleIndex];
                obstacles.Add(new JumpObstacleData(
                    obstacle.ObstacleType,
                    obstacle.IsBottomLine,
                    obstacle.LeftX - fireShift,
                    obstacle.RightX - fireShift,
                    obstacle.CenterX - fireShift));
            }

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                jumpShift,
                jumpShift,
                damageBigAliveWithoutYByReach: true);

            JumpResolveResult result = JumpOutcomeResolver.ResolveJump(obstacles, context);
            return result.State is HamsterStateEnum.Jump or HamsterStateEnum.JumpOver;
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
    }
}
