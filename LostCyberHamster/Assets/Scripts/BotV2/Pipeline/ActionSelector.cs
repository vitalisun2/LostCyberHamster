using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Выбирает лучший шаг из набора безопасных кандидатов.
    /// Этап 4: life collectible > target > other collectible > threat safety,
    /// затем профит и затем энергоэффективность.
    /// </summary>
    public class ActionSelector
    {
        public ChainStep Select(List<ChainStep> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            ChainStep best = null;
            ChainStep second = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (best == null)
                {
                    best = candidate;
                    continue;
                }

                if (Compare(candidate, best) > 0)
                {
                    second = best;
                    best = candidate;
                    continue;
                }

                if (second == null || Compare(candidate, second) > 0)
                {
                    second = candidate;
                }
            }

            BotLogger.Log(BotLogLevel.Verbose,
                "[SELECT] ranking details\n" +
                DescribeCandidate("winner", best) + "\n" +
                DescribeCandidate("runner-up", second));

            return best;
        }

        // >0 означает, что a лучше b; <0 — хуже; 0 — эквивалентны.
        private static int Compare(ChainStep a, ChainStep b)
        {
            if (a.Rank != b.Rank)
                return b.Rank.CompareTo(a.Rank);

            if (a.ProfitScore != b.ProfitScore)
                return a.ProfitScore.CompareTo(b.ProfitScore);

            if (a.EnergyCost != b.EnergyCost)
                return b.EnergyCost.CompareTo(a.EnergyCost);

            // Для стабильности выбора при полном равенстве предпочитаем более раннее исполнение.
            return a.ExecuteAtDistance.CompareTo(b.ExecuteAtDistance);
        }

        private static string DescribeCandidate(string role, ChainStep step)
        {
            if (step == null)
                return $"  {role}: none";

            var sb = new StringBuilder();
            sb.Append("  ").Append(role).Append(": ");
            sb.Append(step.Action).Append(" ");
            sb.Append("rank=").Append(step.Rank).Append(' ');
            sb.Append("profit=").Append(step.ProfitScore).Append(' ');
            sb.Append("energy=").Append(step.EnergyCost).Append(' ');
            sb.Append("execAt=").Append(step.ExecuteAtDistance.ToString("F2")).Append(' ');
            sb.Append("reason=\"").Append(step.Reason).Append("\"");
            return sb.ToString();
        }
    }
}
