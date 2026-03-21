using System.Text;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Выбирает лучшую ветвь действий для текущего снимка.
    /// Генерация действий -> построение ветвей -> оценка -> выбор лучшей.
    /// Чистый класс — не MonoBehaviour.
    /// </summary>
    public class BranchSelector
    {
        private readonly ActionGenerator _actionGenerator = new ActionGenerator();
        private readonly BranchGenerator _branchGenerator = new BranchGenerator();

        private int _lastPlanTargetId;

        /// <summary>
        /// Возвращает лучшую ветвь действий для текущего снимка, или null если действовать не нужно.
        /// </summary>
        public BranchCandidate Replan(BotSceneSnapshot snapshot, ObjectClassifier classifier)
        {
            var actions = _actionGenerator.Generate(snapshot);
            var branches = _branchGenerator.Generate(snapshot, actions, classifier, _actionGenerator);
            var best = BranchEvaluator.SelectBest(branches);

            if (best == null)
            {
                if (_lastPlanTargetId != 0)
                {
                    DebugManager.DiagLog("[BotV3 PLAN] Cleared — no viable branches");
                    _lastPlanTargetId = 0;
                }

                return null;
            }

            int newTargetId = best.Steps[0].TargetObstacle.StableId;
            if (newTargetId != _lastPlanTargetId)
            {
                _lastPlanTargetId = newTargetId;
                LogPlanSelected(best);
            }

            return best;
        }

        public void Reset()
        {
            _lastPlanTargetId = 0;
        }

        private static void LogPlanSelected(BranchCandidate branch)
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
    }
}
