using System;

namespace Assets.Scripts.Bot.PlanState
{
    /// <summary>
    /// Описывает одно запланированное действие бота и его параметры.
    /// </summary>
    public sealed class PlannedAction
    {
        private const float EqualityEpsilon = 0.001f;

        /// <summary>
        /// Создает описание одного действия внутри плана.
        /// </summary>
        public PlannedAction(
            BotActionKind kind,
            float triggerX,
            float renderWorldX,
            float completionWorldShift,
            int targetObstacleIndex,
            int? targetObstacleInstanceId = null,
            bool? targetBottomLine = null,
            int energyCost = 0,
            string description = null)
        {
            Kind = kind;
            TriggerX = triggerX;
            RenderWorldX = renderWorldX;
            CompletionWorldShift = completionWorldShift;
            TargetObstacleIndex = targetObstacleIndex;
            TargetObstacleInstanceId = targetObstacleInstanceId;
            TargetBottomLine = targetBottomLine;
            EnergyCost = energyCost;
            Description = description;
        }

        public BotActionKind Kind { get; }
        public float TriggerX { get; }
        public float RenderWorldX { get; }
        public float CompletionWorldShift { get; }
        public int TargetObstacleIndex { get; }
        public int? TargetObstacleInstanceId { get; }
        public bool? TargetBottomLine { get; }
        public int EnergyCost { get; }
        public string Description { get; }

        /// <summary>
        /// Сравнивает два действия по их planning-параметрам.
        /// </summary>
        public bool IsEquivalentTo(PlannedAction other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other == null)
                return false;

            return Kind == other.Kind
                && Math.Abs(TriggerX - other.TriggerX) <= EqualityEpsilon
                && Math.Abs(RenderWorldX - other.RenderWorldX) <= EqualityEpsilon
                && Math.Abs(CompletionWorldShift - other.CompletionWorldShift) <= EqualityEpsilon
                && TargetObstacleIndex == other.TargetObstacleIndex
                && TargetObstacleInstanceId == other.TargetObstacleInstanceId
                && TargetBottomLine == other.TargetBottomLine
                && EnergyCost == other.EnergyCost;
        }
    }
}
