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
                  .Append(", fire=")
                  .Append(s.FireWorldShift.ToString("F1"))
                  .Append(", window=")
                  .Append(s.EarliestFireWorldShift.ToString("F1"))
                  .Append("..")
                  .Append(s.LatestFireWorldShift.ToString("F1"))
                  .Append(", id=")
                  .Append(s.TargetObstacle.StableId)
                  .Append(" reason=\"").Append(s.Reason).Append("\")");
            }

            sb.Append(" | safe=").Append(branch.Outcome.AllStepsSafe)
              .Append(" slack=")
              .Append(float.IsPositiveInfinity(branch.Outcome.FreeRunAfterFirstStep)
                  ? "inf"
                  : branch.Outcome.FreeRunAfterFirstStep.ToString("F1"))
              .Append(" energy=").Append(branch.Outcome.TotalEnergyCost);
            DebugManager.DiagLog(sb.ToString());
        }

        public static void LogPlanCleared()
        {
            DebugManager.DiagLog("[BotV3 PLAN] Cleared — no viable branches");
        }

        public static void LogPlanDeferred(BranchStep step, float decisionWorldShift)
        {
            DebugManager.DiagLog(
                $"[BotV3 PLAN] Deferred commit: {step.Action}" +
                $" id={step.TargetObstacle.StableId}" +
                $" fire={step.FireWorldShift:F1}" +
                $" deferUntil={decisionWorldShift:F1}");
        }

        public static void LogActionCandidates(
            ObstacleInfo obstacle,
            bool hasSwitchLane,
            bool hasJump,
            string switchLaneRejectReason,
            BotSceneSnapshot snapshot,
            string logScope)
        {
            var sb = new StringBuilder(128);
            sb.Append("[BotV3 GEN] ").Append(obstacle.Type)
              .Append(" id=").Append(obstacle.StableId)
              .Append(" dist=").Append(obstacle.DistanceToHamster.ToString("F1"))
              .Append(" left=").Append(obstacle.LeftX.ToString("F2"))
              .Append(" right=").Append(obstacle.RightX.ToString("F2"))
              .Append(" lane=").Append(obstacle.IsTopLane ? "top" : "bottom")
              .Append(" hamster=").Append(snapshot.HamsterOnBottom ? "bottom" : "top")
              .Append(" energy=").Append(snapshot.Energy)
              .Append(" scope=").Append(string.IsNullOrEmpty(logScope) ? "unspecified" : logScope)
              .Append(" | SwitchLane=").Append(hasSwitchLane)
              .Append(" Jump=").Append(hasJump);

            if (!hasSwitchLane && switchLaneRejectReason != null)
            {
                sb.Append(" [SwitchLane rejected: ").Append(switchLaneRejectReason).Append("]");
            }

            DebugManager.DiagLog(sb.ToString());
        }
    }
}
