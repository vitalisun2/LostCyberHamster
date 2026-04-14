using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanEvaluator
    {
        public PlannedAction SelectBest(IReadOnlyList<PlannedAction> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            return candidates
                .OrderBy(action => action.EnergyCost)
                .ThenByDescending(action => action.TriggerX)
                .First();
        }

        public float Score(IReadOnlyList<PlannedAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return 0f;

            int totalEnergyCost = 0;
            for (int index = 0; index < actions.Count; index++)
                totalEnergyCost += actions[index].EnergyCost;

            return -totalEnergyCost;
        }
    }
}
