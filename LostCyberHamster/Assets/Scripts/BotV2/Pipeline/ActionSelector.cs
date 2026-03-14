using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Выбирает лучший шаг из набора безопасных кандидатов.
    /// Этап 1: выбираем по минимальной стоимости энергии.
    /// SwitchLane (0) предпочтительнее Jump (10).
    /// </summary>
    public class ActionSelector
    {
        public ChainStep Select(List<ChainStep> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            ChainStep best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (best == null || candidates[i].EnergyCost < best.EnergyCost)
                    best = candidates[i];
            }
            return best;
        }
    }
}
