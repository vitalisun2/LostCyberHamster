using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Перепроверяет сохраненный roof switch-lane exit action.
    /// </summary>
    internal sealed class RoofSwitchLaneExitRetainedActionValidator : IRetainedActionValidator
    {
        private readonly RoofSwitchLaneExitPolicy _policy;
        private readonly RoofSwitchLaneExitPlanner _planner;

        public RoofSwitchLaneExitRetainedActionValidator(
            RoofSwitchLaneExitPolicy policy,
            RoofSwitchLaneExitPlanner planner)
        {
            _policy = policy;
            _planner = planner;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает true, если сохраненный сход с крыши через смену линии ещё валиден.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null
                || context.Action == null
                || context.Action.Kind != ActionKind)
            {
                return false;
            }

            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return false;

            return _planner.IsActionStillValid(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                context.DecisionPoint,
                context.Action,
                runFromRoofTravel);
        }
    }
}
