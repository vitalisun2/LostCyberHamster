using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Текущий активный план: выбранная цепочка шагов.
    /// Исполняется только head, хвост доступен для дебага.
    /// </summary>
    public class CurrentPlan
    {
        public List<ChainStep> Steps { get; } = new List<ChainStep>();
        public string Strategy { get; private set; }

        public ChainStep Head => Steps.Count > 0 ? Steps[0] : null;
        public bool IsEmpty => Steps.Count == 0;

        public void ReplaceFrom(ChainCandidate chain, string strategy)
        {
            Steps.Clear();
            if (chain?.Steps != null)
                Steps.AddRange(chain.Steps);
            Strategy = strategy;
        }

        public void RemoveCompletedFromHead()
        {
            while (Steps.Count > 0 && Steps[0].Status == ChainStepStatus.Completed)
                Steps.RemoveAt(0);
        }

        public void Clear()
        {
            Steps.Clear();
            Strategy = null;
        }
    }
}
