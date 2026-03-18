using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Текущий активный план BotV2: выбранная цепочка шагов, из которой
    /// исполняется только head, а хвост остаётся доступным для логики и дебага.
    /// </summary>
    public class CurrentPlan
    {
        public List<ChainStep> Steps { get; } = new List<ChainStep>();

        /// <summary>Краткая причина выбора плана для диагностики.</summary>
        public string Strategy { get; private set; }

        /// <summary>Первый ожидающий выполнения шаг, либо null если план пуст.</summary>
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