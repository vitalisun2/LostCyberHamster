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

        public static void LogActionCandidates(
            ObstacleInfo obstacle,
            bool hasSwitchLane,
            bool hasJump,
            BotSceneSnapshot snapshot)
        {
            var sb = new StringBuilder(128);
            sb.Append("[BotV3 GEN] ").Append(obstacle.Type)
              .Append(" dist=").Append(obstacle.DistanceToHamster.ToString("F1"))
              .Append(" lane=").Append(obstacle.IsTopLane ? "top" : "bottom")
              .Append(" hamster=").Append(snapshot.HamsterOnBottom ? "bottom" : "top")
              .Append(" energy=").Append(snapshot.Energy)
              .Append(" | SwitchLane=").Append(hasSwitchLane)
              .Append(" Jump=").Append(hasJump);

            if (!hasSwitchLane)
            {
                sb.Append(" [SwitchLane rejected: ");
                if (obstacle.DistanceToHamster < 1.5f)
                    sb.Append("too close");
                else
                    sb.Append("lane unsafe at execute distance");
                sb.Append("]");
            }

            DebugManager.DiagLog(sb.ToString());
        }
    }
}
