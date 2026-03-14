using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Выбирает лучший шаг из набора безопасных кандидатов.
    /// Этап 3: приоритеты выбора — профит, затем энергоэффективность.
    /// </summary>
    public class ActionSelector
    {
        public ChainStep Select(List<ChainStep> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            ChainStep best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (best == null)
                {
                    best = candidate;
                    continue;
                }

                if (candidate.ProfitScore > best.ProfitScore)
                {
                    best = candidate;
                    continue;
                }

                if (candidate.ProfitScore == best.ProfitScore && candidate.EnergyCost < best.EnergyCost)
                {
                    best = candidate;
                }
            }
            return best;
        }
    }
}
