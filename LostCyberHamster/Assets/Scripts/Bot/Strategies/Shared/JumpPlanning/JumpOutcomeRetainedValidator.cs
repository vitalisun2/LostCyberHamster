using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет retained action через ожидаемый jump outcome.
    /// </summary>
    internal sealed class JumpOutcomeRetainedValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly JumpOutcomeFireWindowCalculator _fireWindowCalculator;

        public JumpOutcomeRetainedValidator(
            BotActionKind actionKind,
            JumpOutcomeFireWindowCalculator fireWindowCalculator)
        {
            ActionKind = actionKind;
            _fireWindowCalculator = fireWindowCalculator;
        }

        public BotActionKind ActionKind { get; }

        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            return _fireWindowCalculator.IsScheduledFireShiftStillValid(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                context.TargetObstacle,
                context.TargetObstacleIndex,
                context.Action,
                ValidationEpsilon);
        }
    }
}
