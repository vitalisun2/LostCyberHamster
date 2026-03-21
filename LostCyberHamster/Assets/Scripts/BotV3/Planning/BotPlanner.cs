using System.Text;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Принимает решения: какой план действий выбрать.
    /// Генерация действий → построение цепочек → оценка → выбор лучшей.
    /// Чистый класс — не MonoBehaviour.
    /// </summary>
    public class BotPlanner
    {
        private readonly ActionGenerator _actionGenerator = new ActionGenerator();
        private readonly ChainGenerator _chainGenerator = new ChainGenerator();

        private int _lastPlanTargetId;

        /// <summary>
        /// Возвращает лучшую цепочку действий для текущего снимка, или null если действовать не нужно.
        /// </summary>
        public ChainCandidate Replan(BotSceneSnapshot snapshot, ObjectClassifier classifier)
        {
            var actions = _actionGenerator.Generate(snapshot);
            var chains = _chainGenerator.Generate(snapshot, actions, classifier, _actionGenerator);
            var best = BranchEvaluator.SelectBest(chains);

            if (best == null)
            {
                if (_lastPlanTargetId != 0)
                {
                    DebugManager.DiagLog("[BotV3 PLAN] Cleared — no viable chains");
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

        private static void LogPlanSelected(ChainCandidate chain)
        {
            var sb = new StringBuilder(128);
            sb.Append("[BotV3 PLAN] Selected: ");
            for (int i = 0; i < chain.Steps.Count; i++)
            {
                if (i > 0) sb.Append(" -> ");
                var s = chain.Steps[i];
                sb.Append(s.Action).Append("(execAt=")
                  .Append(s.ExecuteAtDistance.ToString("F1"))
                  .Append(" reason=\"").Append(s.Reason).Append("\")");
            }

            sb.Append(" | safe=").Append(chain.Outcome.AllStepsSafe)
              .Append(" energy=").Append(chain.Outcome.TotalEnergyCost);
            DebugManager.DiagLog(sb.ToString());
        }
    }
}
