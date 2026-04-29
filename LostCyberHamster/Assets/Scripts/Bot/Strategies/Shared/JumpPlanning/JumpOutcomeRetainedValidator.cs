using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет retained jump-action через strategy-specific fire-shift validator.
    /// </summary>
    internal sealed class JumpOutcomeRetainedValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly IJumpScheduledFireShiftValidator _fireShiftValidator;

        public JumpOutcomeRetainedValidator(
            BotActionKind actionKind,
            IJumpScheduledFireShiftValidator fireShiftValidator)
        {
            ActionKind = actionKind;
            _fireShiftValidator = fireShiftValidator;
        }

        public BotActionKind ActionKind { get; }

        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            return _fireShiftValidator.IsScheduledFireShiftStillValid(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                context.TargetObstacle,
                context.TargetObstacleIndex,
                context.Action,
                ValidationEpsilon);
        }
    }
}
