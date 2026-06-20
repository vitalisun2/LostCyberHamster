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
            float postFireWorldShift,
            int targetObstacleIndex,
            int? targetObstacleInstanceId = null,
            int? triggerObstacleInstanceId = null,
            bool? targetBottomLine = null,
            int energyCost = 0,
            string description = null,
            int? resultRoofSupportInstanceId = null,
            bool fulfillsJumpOnObjective = false,
            bool isOppositeLaneEntry = false,
            ActionTriggerWindow? triggerWindow = null,
            CollectibleObjectiveValue? collectibleObjectiveValue = null)
        {
            Kind = kind;
            TriggerX = triggerX;
            RenderWorldX = renderWorldX;
            CompletionWorldShift = completionWorldShift;
            PostFireWorldShift = postFireWorldShift;
            TargetObstacleIndex = targetObstacleIndex;
            TargetObstacleInstanceId = targetObstacleInstanceId;
            TriggerObstacleInstanceId = triggerObstacleInstanceId;
            TargetBottomLine = targetBottomLine;
            EnergyCost = energyCost;
            Description = description;
            ResultRoofSupportInstanceId = resultRoofSupportInstanceId;
            FulfillsJumpOnObjective = fulfillsJumpOnObjective;
            IsOppositeLaneEntry = isOppositeLaneEntry;
            TriggerWindow = triggerWindow;
            CollectibleObjectiveValue = collectibleObjectiveValue ?? CollectibleObjectiveValue.None;
        }

        public BotActionKind Kind { get; }
        public float TriggerX { get; }
        public float RenderWorldX { get; }
        public float CompletionWorldShift { get; }
        public float PostFireWorldShift { get; }
        public int TargetObstacleIndex { get; }
        public int? TargetObstacleInstanceId { get; }
        public int? TriggerObstacleInstanceId { get; }
        public bool? TargetBottomLine { get; }
        public int EnergyCost { get; }
        public string Description { get; }
        public int? ResultRoofSupportInstanceId { get; }
        public bool FulfillsJumpOnObjective { get; }
        public bool IsOppositeLaneEntry { get; }
        public ActionTriggerWindow? TriggerWindow { get; }
        public CollectibleObjectiveValue CollectibleObjectiveValue { get; }
        public bool FulfillsCollectibleObjective => CollectibleObjectiveValue.HasValue;

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
                && Math.Abs(PostFireWorldShift - other.PostFireWorldShift) <= EqualityEpsilon
                && TargetObstacleIndex == other.TargetObstacleIndex
                && TargetObstacleInstanceId == other.TargetObstacleInstanceId
                && TriggerObstacleInstanceId == other.TriggerObstacleInstanceId
                && TargetBottomLine == other.TargetBottomLine
                && EnergyCost == other.EnergyCost
                && ResultRoofSupportInstanceId == other.ResultRoofSupportInstanceId
                && FulfillsJumpOnObjective == other.FulfillsJumpOnObjective
                && IsOppositeLaneEntry == other.IsOppositeLaneEntry
                && AreTriggerWindowsEquivalent(TriggerWindow, other.TriggerWindow)
                && CollectibleObjectiveValue.Kind == other.CollectibleObjectiveValue.Kind
                && CollectibleObjectiveValue.EffectiveGain == other.CollectibleObjectiveValue.EffectiveGain;
        }

        private static bool AreTriggerWindowsEquivalent(
            ActionTriggerWindow? left,
            ActionTriggerWindow? right)
        {
            if (!left.HasValue || !right.HasValue)
                return left.HasValue == right.HasValue;

            return Math.Abs(left.Value.EarliestTriggerX - right.Value.EarliestTriggerX) <= EqualityEpsilon
                && Math.Abs(left.Value.LatestTriggerX - right.Value.LatestTriggerX) <= EqualityEpsilon;
        }
    }
}
