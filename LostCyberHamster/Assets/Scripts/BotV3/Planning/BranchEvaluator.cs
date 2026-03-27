using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Оценка и выбор лучшей ветви планировщика.
    /// Порядок сравнения: safety -> energy -> branch depth -> first fire timing.
    /// </summary>
    public static class BranchEvaluator
    {
        public static BranchCandidate SelectBest(List<BranchCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            candidates.Sort(CompareCandidates);
            return candidates[0];
        }

        public static int CompareCandidates(BranchCandidate a, BranchCandidate b)
        {
            var oa = a.Outcome;
            var ob = b.Outcome;

            if (oa.AllStepsSafe != ob.AllStepsSafe)
                return oa.AllStepsSafe ? -1 : 1;

            if (oa.TotalEnergyCost != ob.TotalEnergyCost)
                return oa.TotalEnergyCost.CompareTo(ob.TotalEnergyCost);

            int depthCompare = a.Steps.Count.CompareTo(b.Steps.Count);
            if (depthCompare != 0)
                return depthCompare;

            return a.Steps[0].FireWorldShift.CompareTo(b.Steps[0].FireWorldShift);
        }
    }
}
