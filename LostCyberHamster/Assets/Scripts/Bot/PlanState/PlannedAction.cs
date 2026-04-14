namespace Assets.Scripts.Bot.PlanState
{
    public sealed class PlannedAction
    {
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
    }
}
