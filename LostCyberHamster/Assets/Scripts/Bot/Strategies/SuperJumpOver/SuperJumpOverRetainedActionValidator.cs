using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный super jump-over action.
    /// </summary>
    internal sealed class SuperJumpOverRetainedActionValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly SuperJumpOverScheduledFireShiftValidator _fireShiftValidator;

        /// <summary>
        /// Создаёт retained validator с локальной проверкой scheduled fire shift.
        /// </summary>
        public SuperJumpOverRetainedActionValidator(SuperJumpOverScheduledFireShiftValidator fireShiftValidator)
        {
            _fireShiftValidator = fireShiftValidator;
        }

        /// <summary>
        /// Тип действия, которое умеет сохранять validator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpOver;

        /// <summary>
        /// Проверяет, что сохранённый action всё ещё соответствует текущему planning context.
        /// </summary>
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
