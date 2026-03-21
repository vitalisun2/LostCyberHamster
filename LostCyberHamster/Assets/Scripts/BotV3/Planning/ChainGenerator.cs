using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Строит ветви планировщика до заданной глубины.
    /// V3 minimal: SwitchLane chains, depth 1-2.
    /// </summary>
    public class ChainGenerator
    {
        private const int MaxBranchDepth = 3;
        private readonly StateProjector _stateProjector = new StateProjector();

        public List<ChainCandidate> Generate(
            BotSceneSnapshot snapshot,
            List<ChainStep> firstStepCandidates,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator)
        {
            var result = new List<ChainCandidate>();
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
                    first,
                    depth: 1,
                    stepsSoFar: new List<ChainStep>(),
                    originalSnapshot: snapshot,
                    result: result);
            }

            return result;
        }

        private void ExploreBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ChainStep step,
            int depth,
            List<ChainStep> stepsSoFar,
            BotSceneSnapshot originalSnapshot,
            List<ChainCandidate> result)
        {
            stepsSoFar.Add(step);

            var projection = _stateProjector.Project(snapshot, step);
            if (!projection.IsSafe || projection.NextState == null)
                return;

            var projectedSnapshot = projection.NextState.ToSnapshot();
            classifier.Classify(projectedSnapshot);

            result.Add(BuildCandidate(stepsSoFar, originalSnapshot));

            if (depth >= MaxBranchDepth)
                return;

            var nextSteps = actionGenerator.Generate(projectedSnapshot);
            for (int i = 0; i < nextSteps.Count; i++)
            {
                var next = nextSteps[i];
                if (next == null) continue;

                ExploreBranch(
                    projectedSnapshot,
                    classifier,
                    actionGenerator,
                    next,
                    depth + 1,
                    new List<ChainStep>(stepsSoFar),
                    originalSnapshot,
                    result);
            }
        }

        private static ChainCandidate BuildCandidate(
            List<ChainStep> steps,
            BotSceneSnapshot originalSnapshot)
        {
            int totalEnergyCost = 0;
            for (int i = 0; i < steps.Count; i++)
                totalEnergyCost += steps[i].EnergyCost;

            return new ChainCandidate
            {
                Steps = new List<ChainStep>(steps),
                Outcome = new BranchOutcome
                {
                    TotalEnergyCost = totalEnergyCost,
                    AllStepsSafe = IsIdlePeriodSafe(originalSnapshot, steps[0])
                }
            };
        }

        private static bool IsIdlePeriodSafe(BotSceneSnapshot snapshot, ChainStep firstStep)
        {
            float waitTravel = firstStep.TargetObstacle.DistanceToHamster - firstStep.ExecuteAtDistance;
            if (waitTravel <= 0f)
                return true;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat) continue;
                if (!ActionGenerator.IsOnSameLane(snapshot, obstacle)) continue;
                if (obstacle.DistanceToHamster <= 0f) continue;
                if (obstacle.StableId == firstStep.TargetObstacle.StableId) continue;

                if (obstacle.DistanceToHamster < waitTravel)
                    return false;
            }

            return true;
        }
    }
}
