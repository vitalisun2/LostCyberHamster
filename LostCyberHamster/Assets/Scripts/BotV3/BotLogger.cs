using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Централизованное логирование BotV3.
    /// Форматирует и выводит диагностические сообщения через DebugManager.
    /// </summary>
    public static class BotLogger
    {
        private const int DefaultBuilderCapacity = 128;

        [global::System.ThreadStatic]
        private static StringBuilder _sharedBuilder;

        public static void LogPlanSelected(BranchCandidate branch)
        {
            var sb = AcquireBuilder();
            sb.Append("[BotV3 PLAN] Selected: ");
            for (int i = 0; i < branch.Steps.Count; i++)
            {
                if (i > 0) sb.Append(" -> ");
                var s = branch.Steps[i];
                sb.Append(s.Action).Append("(execAt=")
                  .Append(s.ExecuteAtDistance.ToString("F1"))
                  .Append(", fire=")
                  .Append(s.FireWorldShift.ToString("F1"))
                  .Append(", id=")
                  .Append(s.TargetObstacle.StableId)
                  .Append(" reason=\"").Append(s.Reason).Append("\")");
            }

            sb.Append(" | safe=").Append(branch.Outcome.AllStepsSafe)
              .Append(" energy=").Append(branch.Outcome.TotalEnergyCost)
              .Append(" depth=").Append(branch.Steps.Count);
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
            IReadOnlyList<(BotAction action, bool added, string rejectReason)> candidates,
            BotSceneSnapshot snapshot,
            string logScope)
        {
            var sb = AcquireBuilder();
            sb.Append("[BotV3 GEN] ").Append(obstacle.Type)
              .Append(" id=").Append(obstacle.StableId)
              .Append(" dist=").Append(obstacle.DistanceToHamster.ToString("F1"))
              .Append(" left=").Append(obstacle.LeftX.ToString("F2"))
              .Append(" right=").Append(obstacle.RightX.ToString("F2"))
              .Append(" lane=").Append(obstacle.IsTopLane ? "top" : "bottom")
              .Append(" hamster=").Append(snapshot.HamsterOnBottom ? "bottom" : "top")
              .Append(" energy=").Append(snapshot.Energy)
              .Append(" scope=").Append(string.IsNullOrEmpty(logScope) ? "unspecified" : logScope)
              .Append(" |");

            foreach (var (action, added, _) in candidates)
                sb.Append(' ').Append(action).Append('=').Append(added);

            foreach (var (action, added, rejectReason) in candidates)
                if (!added && !string.IsNullOrEmpty(rejectReason))
                    sb.Append(" [").Append(action).Append(" rejected: ").Append(rejectReason).Append(']');

            DebugManager.DiagLog(sb.ToString());
        }

        private static StringBuilder AcquireBuilder()
        {
            if (_sharedBuilder == null)
                _sharedBuilder = new StringBuilder(DefaultBuilderCapacity);
            else
                _sharedBuilder.Clear();

            return _sharedBuilder;
        }
    }
}
