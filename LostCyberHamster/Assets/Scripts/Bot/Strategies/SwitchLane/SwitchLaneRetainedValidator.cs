using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранное действие смены линии на границе retained-префикса плана.
    /// </summary>
    internal sealed class SwitchLaneRetainedValidator : IRetainedActionValidator
    {
        private const float _validationEpsilon = 0.0001f;

        private readonly SwitchLaneSpecification _specification;
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;

        public SwitchLaneRetainedValidator(
            SwitchLaneSpecification specification,
            SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _specification = specification;
            _fireWindowCalculator = fireWindowCalculator;
        }

        /// <summary>
        /// Возвращает тип действия, для которого validator перепроверяет сохранённый план.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SwitchLane;

        /// <summary>
        /// Проверяет, что сохранённое действие смены линии всё ещё соответствует текущему planning context и остаётся внутри допустимого окна запуска.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Отсекает неподходящий retained action.
            if (context == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || !context.Action.TargetBottomLine.HasValue)
            {
                return false;
            }

            // Проверяет, что стратегия смены линии всё ещё применима к текущему состоянию.
            if (!_specification.IsSatisfiedBy(context.PlanningState))
                return false;

            if (context.Action.TargetBottomLine.Value == context.PlanningState.Hamster.IsOnBottomLine)
                return false;

            // Пересчитывает верхнюю границу допустимого fire window.
            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    context.PlanningState.Hamster,
                    context.TargetObstacle,
                    out float latestFireShift))
            {
                return false;
            }

            // Восстанавливает текущий fire shift сохранённого действия.
            float projectedTriggerX = context.Action.TriggerX - context.PlanningState.ProjectionWorldShift;
            float fireShift = context.TargetObstacle.LeftX - projectedTriggerX;
            if (fireShift < 0f || fireShift > latestFireShift + _validationEpsilon)
                return false;

            // Проверяет попадание fire shift в одно из безопасных окон смены линии.
            List<SafeInterval> safeIntervals = _fireWindowCalculator.CollectSafeFireIntervals(
                context.ProjectedWorldSnapshot,
                context.PlanningState.Hamster,
                context.Action.TargetBottomLine.Value,
                latestFireShift);

            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval interval = safeIntervals[intervalIndex];
                if (fireShift >= interval.Start - _validationEpsilon
                    && fireShift <= interval.End + _validationEpsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
