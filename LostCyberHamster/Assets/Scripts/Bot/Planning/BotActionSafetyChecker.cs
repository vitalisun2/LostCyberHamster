using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

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
                BotActionKind.Jump => IsSafeJump(state, action, world),
                BotActionKind.SuperJump => IsSafeSuperJump(state, action, world),
                BotActionKind.Tap => IsSafeTap(state, action),
                _ => false
            };
        }

        private static bool IsSafeJump(PlanningState state, PlannedAction action, WorldSnapshot world)
        {
            return true;
        }

        private static bool IsSafeSuperJump(PlanningState state, PlannedAction action, WorldSnapshot world)
        {
            return true;
        }

        private static bool IsSafeTap(PlanningState state, PlannedAction action)
        {
            return action.TargetBottomLine.HasValue
                && action.TargetBottomLine.Value != state.IsOnBottomLine;
        }
    }
}
