namespace Assets.Scripts.Bot.PlanState
{
    public sealed class CommittedPlan
    {
        public BotPlan Current { get; private set; } = BotPlan.Empty();

        public float CommittedBoundaryX => Current.CommittedBoundaryX;

        public void Replace(BotPlan plan)
        {
            Current = plan ?? BotPlan.Empty();
        }

        public void Clear(float committedBoundaryX = 0f)
        {
            Current = BotPlan.Empty(committedBoundaryX);
        }
    }
}
