using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Execution
{
    public sealed class PlanExecutor
    {
        public BotPlan CurrentPlan { get; private set; } = BotPlan.Empty();

        public void SetPlan(BotPlan plan)
        {
            CurrentPlan = plan ?? BotPlan.Empty();
        }

        public void Clear(float committedBoundaryX = 0f)
        {
            CurrentPlan = BotPlan.Empty(committedBoundaryX);
        }

        public void Tick()
        {
        }
    }
}
