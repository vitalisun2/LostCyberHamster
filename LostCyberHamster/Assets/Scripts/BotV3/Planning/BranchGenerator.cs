using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Строит ветви планировщика до заданной глубины.
    /// V3 minimal: SwitchLane branches, depth 1-2.
    /// </summary>
    public class BranchGenerator
    {
        private const int MaxBranchDepth = 3;
        private readonly StateProjector _stateProjector = new StateProjector();

        public List<BranchCandidate> Generate(
            BotSceneSnapshot snapshot,
            List<BranchStep> firstStepCandidates,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator)
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
                    first,
                    depth: 1,
                    stepsSoFar: new List<BranchStep>(),
                    originalSnapshot: snapshot,
                    result: result);
            }

            return result;
        }

        private void ExploreBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            BranchStep step,
            int depth,
            List<BranchStep> stepsSoFar,
            BotSceneSnapshot originalSnapshot,
            List<BranchCandidate> result)
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
                    new List<BranchStep>(stepsSoFar),
                    originalSnapshot,
                    result);
            }
        }

        private static BranchCandidate BuildCandidate(
            List<BranchStep> steps,
            BotSceneSnapshot originalSnapshot)
        {
            int totalEnergyCost = 0;
            for (int i = 0; i < steps.Count; i++)
                totalEnergyCost += steps[i].EnergyCost;

            return new BranchCandidate
            {
                Steps = new List<BranchStep>(steps),
                Outcome = new BranchOutcome
                {
                    TotalEnergyCost = totalEnergyCost,
                    AllStepsSafe = IsIdlePeriodSafe(originalSnapshot, steps[0])
                }
            };
        }

        private static bool IsIdlePeriodSafe(BotSceneSnapshot snapshot, BranchStep firstStep)
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
