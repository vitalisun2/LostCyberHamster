using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Строит ветви планировщика до заданной глубины.
    /// Для каждого safe шага проецирует мир, находит следующую проблему и продолжает ветку рекурсивно.
    /// </summary>
    public class BranchGenerator
    {
        private const int MaxBranchDepth = 3;
        private readonly BranchStep[] _stepBuffer = new BranchStep[MaxBranchDepth];

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

            if (stepCount >= MaxBranchDepth)
            {
                AddCurrentCandidate(result, stepCount, originalSnapshot);
                return;
            }

            if (!TryResolveNextProblem(projectedSnapshot, problemResolver, out var nextProblem))
            {
                AddCurrentCandidate(result, stepCount, originalSnapshot);
                return;
            }

            if (TryExploreChildBranches(
                projectedSnapshot,
                classifier,
                actionGenerator,
                problemResolver,
                nextProblem,
                stepCount,
                originalSnapshot,
                result))
                return;

            AddCurrentCandidate(result, stepCount, originalSnapshot);
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

            projectedSnapshot = classifier.Classify(projection.NextState.ToSnapshot());
            return true;
        }

        private static bool TryResolveNextProblem(
            BotSceneSnapshot projectedSnapshot,
            ProblemResolver problemResolver,
            out ProblemDescriptor nextProblem)
        {
            nextProblem = problemResolver.ResolveNext(projectedSnapshot);
            return nextProblem != null;
        }

        private bool TryExploreChildBranches(
            BotSceneSnapshot projectedSnapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ProblemResolver problemResolver,
            ProblemDescriptor nextProblem,
            int stepCount,
            BotSceneSnapshot originalSnapshot,
            List<BranchCandidate> result)
        {
            var nextSteps = actionGenerator.Generate(
                projectedSnapshot,
                nextProblem,
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
            BotSceneSnapshot originalSnapshot)
        {
            result.Add(BuildCandidate(_stepBuffer, stepCount, originalSnapshot));
        }

        private static BranchCandidate BuildCandidate(
            BranchStep[] buffer,
            int count,
            BotSceneSnapshot originalSnapshot)
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
                    IsIdlePeriodSafe(originalSnapshot, buffer[0])));
        }

        /// <summary>
        /// Проверяет, нет ли same-lane угроз в зоне ожидания до первого fire.
        /// </summary>
        private static bool IsIdlePeriodSafe(BotSceneSnapshot snapshot, BranchStep firstStep)
        {
            // Вычислить расстояние ожидания до fire
            float waitTravel = firstStep.TargetObstacle.DistanceToHamster - firstStep.ExecuteAtDistance;
            if (waitTravel <= 0f)
                return true;

            // Проверить, нет ли угроз ближе порога fire
            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat) continue;
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
