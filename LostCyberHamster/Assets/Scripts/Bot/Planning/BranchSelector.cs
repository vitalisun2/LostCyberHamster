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

        public BranchSelector(
            float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback,
            float jumpOnRoofLandingOffset = BotConsts.JumpOnRoofLandingOffsetFallback)
        {
            _actionGenerator = new ActionGenerator(superJumpLandingOffset, jumpOnRoofLandingOffset);
        }

        /// <summary>
        /// Возвращает лучшую ветвь действий для текущего снимка, или null если действовать не нужно.
        /// </summary>
        public BranchCandidate FindBestBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            List<BranchStep> retainedSteps = null)
        {
            if (!_problemResolver.TryResolveNextThreat(snapshot, classifier, out var target))
                return BuildRetainedCandidate(retainedSteps);

            var actions = _actionGenerator.Generate(snapshot, target, BranchLogScopes.Root);
            return SelectBestActionBranch(snapshot, classifier, actions, retainedSteps);
        }

        private BranchCandidate SelectBestActionBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            List<BranchStep> actions,
            List<BranchStep> retainedSteps)
        {
            var branches = _branchGenerator.Generate(snapshot, actions, classifier, _actionGenerator, _problemResolver);
            var best = BranchEvaluator.SelectBest(branches);
            var retained = FindMatchingRetainedCandidate(branches, retainedSteps);

            if (retained == null)
                return best;

            return BranchEvaluator.IsStrictlyBetterForReplacement(best, retained)
                ? best
                : retained;
        }

        private static BranchCandidate FindMatchingRetainedCandidate(
            List<BranchCandidate> branches,
            List<BranchStep> retainedSteps)
        {
            if (branches == null || retainedSteps == null || retainedSteps.Count == 0)
                return null;

            for (int i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                if (MatchesRetainedPrefix(branch, retainedSteps))
                    return branch;
            }

            return null;
        }

        private static bool MatchesRetainedPrefix(BranchCandidate branch, List<BranchStep> retainedSteps)
        {
            if (branch == null || retainedSteps == null || branch.Steps.Count < retainedSteps.Count)
                return false;

            for (int i = 0; i < retainedSteps.Count; i++)
            {
                var retained = retainedSteps[i];
                var candidate = branch.Steps[i];

                if (candidate.Action != retained.Action)
                    return false;

                if (candidate.TargetObstacle.StableId != retained.TargetObstacle.StableId)
                    return false;
            }

            return true;
        }

        private static BranchCandidate BuildRetainedCandidate(List<BranchStep> retainedSteps)
        {
            if (retainedSteps == null || retainedSteps.Count == 0)
                return null;

            int totalEnergyCost = 0;
            var steps = new List<BranchStep>(retainedSteps.Count);

            for (int i = 0; i < retainedSteps.Count; i++)
            {
                var step = retainedSteps[i];
                totalEnergyCost += step.EnergyCost;
                steps.Add(step);
            }

            return new BranchCandidate(steps, new BranchOutcome(totalEnergyCost, true));
        }
    }
}
