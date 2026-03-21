using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Оценка и выбор лучшей ветви планировщика.
    /// V3 minimal: AllStepsSafe → TotalEnergyCost → длина ветви.
    /// </summary>
    public static class BranchEvaluator
    {
        public static BranchCandidate SelectBest(List<BranchCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            candidates.Sort(Compare);
            return candidates[0];
        }

        private static int Compare(BranchCandidate a, BranchCandidate b)
        {
            var oa = a.Outcome;
            var ob = b.Outcome;

            if (oa.AllStepsSafe != ob.AllStepsSafe)
                return oa.AllStepsSafe ? -1 : 1;

            if (oa.TotalEnergyCost != ob.TotalEnergyCost)
                return oa.TotalEnergyCost.CompareTo(ob.TotalEnergyCost);

            return b.Steps.Count.CompareTo(a.Steps.Count);
        }
    }
}
