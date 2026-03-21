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
        public BranchCandidate FindBestBranch(BotSceneSnapshot snapshot, ObjectClassifier classifier)
        {
            var actions = _actionGenerator.Generate(snapshot);
            var branches = _branchGenerator.Generate(snapshot, actions, classifier, _actionGenerator);
            var best = BranchEvaluator.SelectBest(branches);

            if (best == null)
            {
                if (_lastPlanTargetId != 0)
                {
                    BotLogger.LogPlanCleared();
                    _lastPlanTargetId = 0;
                }

                return null;
            }

            int newTargetId = best.Steps[0].TargetObstacle.StableId;
            if (newTargetId != _lastPlanTargetId)
            {
                _lastPlanTargetId = newTargetId;
                BotLogger.LogPlanSelected(best);
            }

            return best;
        }

        public void Reset()
        {
            _lastPlanTargetId = 0;
        }
    }
}
