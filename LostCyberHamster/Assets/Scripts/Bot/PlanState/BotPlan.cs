using System;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.PlanState
{
    public sealed class BotPlan
    {
        public BotPlan(IReadOnlyList<PlannedAction> actions, float committedBoundaryX, float score = 0f)
        {
            Actions = actions ?? Array.Empty<PlannedAction>();
            CommittedBoundaryX = committedBoundaryX;
            Score = score;
        }

        public IReadOnlyList<PlannedAction> Actions { get; }
        public float CommittedBoundaryX { get; }
        public float Score { get; }
        public bool HasActions => Actions.Count > 0;

        public static BotPlan Empty(float committedBoundaryX = 0f)
        {
            return new BotPlan(Array.Empty<PlannedAction>(), committedBoundaryX);
        }
    }
}
