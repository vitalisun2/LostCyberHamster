using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Проверяет валидность сохранённого SwitchLane action.
    /// </summary>
    internal sealed class SwitchLaneRetainedValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly SwitchLaneSpecification _specification;
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;

        public SwitchLaneRetainedValidator(
            SwitchLaneSpecification specification,
            SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _specification = specification;
            _fireWindowCalculator = fireWindowCalculator;
        }

        public BotActionKind ActionKind => BotActionKind.SwitchLane;

        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || !context.Action.TargetBottomLine.HasValue)
            {
                return false;
            }

            if (context.Action.TargetBottomLine.Value == context.PlanningState.Hamster.IsOnBottomLine)
                return false;

            if (!_specification.IsSatisfiedBy(context.PlanningState, context.TargetObstacle))
                return false;

            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    context.PlanningState.Hamster,
                    context.TargetObstacle,
                    out float latestFireShift))
            {
                return false;
            }

            float projectedTriggerX = context.Action.TriggerX - context.PlanningState.ProjectionWorldShift;
            float fireShift = context.TargetObstacle.LeftX - projectedTriggerX;
            if (fireShift < 0f || fireShift > latestFireShift + ValidationEpsilon)
                return false;

            List<SafeInterval> safeIntervals = _fireWindowCalculator.CollectSafeFireIntervals(
                context.ProjectedWorldSnapshot,
                context.PlanningState.Hamster,
                context.Action.TargetBottomLine.Value,
                latestFireShift);
            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval interval = safeIntervals[intervalIndex];
                if (fireShift >= interval.Start - ValidationEpsilon
                    && fireShift <= interval.End + ValidationEpsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
