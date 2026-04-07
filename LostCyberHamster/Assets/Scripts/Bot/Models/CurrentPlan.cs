using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Текущий активный план: выбранная ветвь шагов.
    /// Исполняется только head, хвост доступен для дебага.
    /// </summary>
    public class CurrentPlan
    {
        public List<BranchStep> Steps { get; } = new List<BranchStep>();
        public string Strategy { get; private set; }

        public BranchStep Head => Steps.Count > 0 ? Steps[0] : null;
        public bool IsEmpty => Steps.Count == 0;

        public void ReplaceFrom(BranchCandidate chain, string strategy)
        {
            Steps.Clear();
            if (chain != null)
                Steps.AddRange(chain.Steps);
            Strategy = strategy;
        }

        public BranchStep AdvanceCompletedHead()
        {
            RemoveCompletedFromHead();
            return Head;
        }

        public List<BranchStep> SnapshotRetainableSteps()
        {
            var retainableSteps = new List<BranchStep>(Steps.Count);

            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                if (step.Status != BranchStepStatus.Ready)
                    break;

                retainableSteps.Add(step);
            }

            return retainableSteps;
        }

        public void RemoveCompletedFromHead()
        {
            while (Steps.Count > 0 && Steps[0].Status == BranchStepStatus.Completed)
                Steps.RemoveAt(0);
        }

        public void Clear()
        {
            Steps.Clear();
            Strategy = null;
        }
    }
}
