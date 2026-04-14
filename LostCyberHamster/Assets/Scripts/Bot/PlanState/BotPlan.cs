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

        public bool IsEquivalentTo(BotPlan other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other == null || Actions.Count != other.Actions.Count)
                return false;

            for (int actionIndex = 0; actionIndex < Actions.Count; actionIndex++)
            {
                if (!Actions[actionIndex].IsEquivalentTo(other.Actions[actionIndex]))
                    return false;
            }

            return true;
        }

        public static BotPlan Empty(float committedBoundaryX = 0f)
        {
            return new BotPlan(Array.Empty<PlannedAction>(), committedBoundaryX);
        }
    }
}
