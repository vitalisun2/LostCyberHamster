using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Выбирает лучшую ветвь действий для текущего снимка.
    /// Генерация действий -> построение ветвей -> оценка -> выбор лучшей.
    /// Чистый класс — не MonoBehaviour.
    /// </summary>
    public class BranchSelector
    {
        private readonly ActionGenerator _actionGenerator;
        private readonly BranchGenerator _branchGenerator = new BranchGenerator();
        private readonly ProblemResolver _problemResolver = new ProblemResolver();

        public BranchSelector(float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback)
        {
            _actionGenerator = new ActionGenerator(superJumpLandingOffset);
        }

        /// <summary>
        /// Возвращает лучшую ветвь действий для текущего снимка, или null если действовать не нужно.
        /// </summary>
        public BranchCandidate FindBestBranch(BotSceneSnapshot snapshot, ObjectClassifier classifier)
        {
            // Определить ближайшую проблему
            var problem = _problemResolver.ResolveNext(snapshot);
            if (problem == null)
                return null;

            // Сгенерировать кандидатов и выбрать лучшую ветку
            var actions = _actionGenerator.Generate(snapshot, problem, BranchLogScopes.Root);
            return SelectBestActionBranch(snapshot, classifier, actions);
        }

        private BranchCandidate SelectBestActionBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            List<BranchStep> actions)
        {
            var branches = _branchGenerator.Generate(snapshot, actions, classifier, _actionGenerator, _problemResolver);
            return BranchEvaluator.SelectBest(branches);
        }
    }
}
