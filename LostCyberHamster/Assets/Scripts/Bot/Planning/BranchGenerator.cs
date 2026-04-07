using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Строит ветви планировщика до заданной глубины.
    /// Для каждого safe шага проецирует мир, находит следующую проблему и продолжает ветку рекурсивно.
    /// </summary>
    public class BranchGenerator
    {
        private readonly BranchStep[] _stepBuffer = new BranchStep[BotConsts.MaxBranchDepth];

        public List<BranchCandidate> Generate(
            BotSceneSnapshot snapshot,
            List<BranchStep> firstStepCandidates,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ProblemResolver problemResolver)
        {
            var result = new List<BranchCandidate>();
            if (firstStepCandidates == null || firstStepCandidates.Count == 0)
                return result;

            for (int i = 0; i < firstStepCandidates.Count; i++)
            {
                var first = firstStepCandidates[i];
                if (first == null) continue;

                ExploreBranch(
                    snapshot,
                    classifier,
                    actionGenerator,
                    problemResolver,
                    first,
                    depth: 0,
                    originalSnapshot: snapshot,
                    result: result);
            }

            return result;
        }

        private void ExploreBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ProblemResolver problemResolver,
            BranchStep step,
            int depth,
            BotSceneSnapshot originalSnapshot,
            List<BranchCandidate> result)
        {
            _stepBuffer[depth] = step;
            int stepCount = depth + 1;

            if (!TryProjectSnapshot(snapshot, classifier, actionGenerator, step, out var projectedSnapshot))
                return;

            if (stepCount >= BotConsts.MaxBranchDepth)
            {
                AddCurrentCandidate(result, stepCount, originalSnapshot, classifier);
                return;
            }

            if (!TryResolveNextThreat(projectedSnapshot, classifier, problemResolver, out var nextThreat))
            {
                AddCurrentCandidate(result, stepCount, originalSnapshot, classifier);
                return;
            }

            if (IsThreatRepeatedInCurrentChain(nextThreat, stepCount))
                return;

            if (TryExploreChildBranches(
                projectedSnapshot,
                classifier,
                actionGenerator,
                problemResolver,
                nextThreat,
                stepCount,
                originalSnapshot,
                result))
                return;

            // Угроза найдена, но ни одна стратегия не может её обработать —
            // ветка не решает проблему, не добавляем.
        }

        private static bool TryProjectSnapshot(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            BranchStep step,
            out BotSceneSnapshot projectedSnapshot)
        {
            var projection = actionGenerator.Project(snapshot, step);
            if (!projection.IsSafe || projection.NextState == null)
            {
                projectedSnapshot = null;
                return false;
            }

            projectedSnapshot = projection.NextState.ToSnapshot();
            return true;
        }

        private static bool TryResolveNextThreat(
            BotSceneSnapshot projectedSnapshot,
            ObjectClassifier classifier,
            ProblemResolver problemResolver,
            out ObstacleInfo nextThreat)
        {
            return problemResolver.TryResolveNextThreat(projectedSnapshot, classifier, out nextThreat);
        }

        /// <summary>
        /// Повтор той же угрозы в одной ветке означает ложный oscillation/zigzag.
        /// Такие ветки не должны считаться safe кандидатами.
        /// </summary>
        private bool IsThreatRepeatedInCurrentChain(ObstacleInfo nextThreat, int stepCount)
        {
            for (int i = 0; i < stepCount; i++)
            {
                if (_stepBuffer[i].TargetObstacle.StableId == nextThreat.StableId)
                    return true;
            }

            return false;
        }

        private bool TryExploreChildBranches(
            BotSceneSnapshot projectedSnapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ProblemResolver problemResolver,
            ObstacleInfo nextThreat,
            int stepCount,
            BotSceneSnapshot originalSnapshot,
            List<BranchCandidate> result)
        {
            var nextSteps = actionGenerator.Generate(
                projectedSnapshot,
                nextThreat,
                BranchLogScopes.ProjectionAtDepth(stepCount));
            int resultCountBeforeChildren = result.Count;

            for (int i = 0; i < nextSteps.Count; i++)
            {
                var next = nextSteps[i];
                if (next == null) continue;

                ExploreBranch(
                    projectedSnapshot,
                    classifier,
                    actionGenerator,
                    problemResolver,
                    next,
                    stepCount,
                    originalSnapshot,
                    result);
            }

            return result.Count != resultCountBeforeChildren;
        }

        private void AddCurrentCandidate(
            List<BranchCandidate> result,
            int stepCount,
            BotSceneSnapshot originalSnapshot,
            ObjectClassifier classifier)
        {
            result.Add(BuildCandidate(_stepBuffer, stepCount, originalSnapshot, classifier));
        }

        private static BranchCandidate BuildCandidate(
            BranchStep[] buffer,
            int count,
            BotSceneSnapshot originalSnapshot,
            ObjectClassifier classifier)
        {
            var steps = new List<BranchStep>(count);
            int totalEnergyCost = 0;
            for (int i = 0; i < count; i++)
            {
                steps.Add(buffer[i]);
                totalEnergyCost += buffer[i].EnergyCost;
            }

            return new BranchCandidate(
                steps,
                new BranchOutcome(
                    totalEnergyCost,
                    IsIdlePeriodSafe(originalSnapshot, buffer[0], classifier)));
        }

        /// <summary>
        /// Проверяет, нет ли same-lane угроз в зоне ожидания до первого fire.
        /// </summary>
        private static bool IsIdlePeriodSafe(
            BotSceneSnapshot snapshot,
            BranchStep firstStep,
            ObjectClassifier classifier)
        {
            float waitTravel = firstStep.TargetObstacle.DistanceToHamster - firstStep.ExecuteAtDistance;
            if (waitTravel <= 0f)
                return true;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (!classifier.IsThreat(obstacle, snapshot)) continue;
                if (!snapshot.IsOnSameLane(obstacle)) continue;
                if (obstacle.DistanceToHamster <= 0f) continue;
                if (obstacle.StableId == firstStep.TargetObstacle.StableId) continue;

                if (obstacle.DistanceToHamster < waitTravel)
                    return false;
            }

            return true;
        }
    }
}
