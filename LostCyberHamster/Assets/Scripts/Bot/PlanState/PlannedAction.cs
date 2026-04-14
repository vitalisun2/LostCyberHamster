namespace Assets.Scripts.Bot.PlanState
{
    public sealed class PlannedAction
    {
        public PlannedAction(BotActionKind kind, float triggerX, int? targetObstacleInstanceId = null, string description = null)
        {
            Kind = kind;
            TriggerX = triggerX;
            TargetObstacleInstanceId = targetObstacleInstanceId;
            Description = description;
        }

        public BotActionKind Kind { get; }
        public float TriggerX { get; }
        public int? TargetObstacleInstanceId { get; }
        public string Description { get; }
    }
}
