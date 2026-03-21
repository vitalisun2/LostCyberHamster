using System.Text;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Централизованное логирование BotV3.
    /// Форматирует и выводит диагностические сообщения через DebugManager.
    /// </summary>
    public static class BotLogger
    {
        public static void LogPlanSelected(BranchCandidate branch)
        {
            var sb = new StringBuilder(128);
            sb.Append("[BotV3 PLAN] Selected: ");
            for (int i = 0; i < branch.Steps.Count; i++)
            {
                if (i > 0) sb.Append(" -> ");
                var s = branch.Steps[i];
                sb.Append(s.Action).Append("(execAt=")
                  .Append(s.ExecuteAtDistance.ToString("F1"))
                  .Append(" reason=\"").Append(s.Reason).Append("\")");
            }

            sb.Append(" | safe=").Append(branch.Outcome.AllStepsSafe)
              .Append(" energy=").Append(branch.Outcome.TotalEnergyCost);
            DebugManager.DiagLog(sb.ToString());
        }

        public static void LogPlanCleared()
        {
            DebugManager.DiagLog("[BotV3 PLAN] Cleared — no viable branches");
        }
    }
}
