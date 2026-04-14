using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class ActionGenerator
    {
        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, BotPerceptionSnapshot perceptionSnapshot)
        {
            return new List<PlannedAction>();
        }
    }
}
