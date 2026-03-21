using System.Collections.Generic;

namespace Assets.Scripts.BotV3
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
            if (chain?.Steps != null)
                Steps.AddRange(chain.Steps);
            Strategy = strategy;
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
