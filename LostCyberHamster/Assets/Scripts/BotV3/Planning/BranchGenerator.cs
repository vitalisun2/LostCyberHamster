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
        private readonly BranchStep[] _stepBuffer = new BranchStep[MaxBranchDepth];

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
            BranchStep step,
            int depth,
            BotSceneSnapshot originalSnapshot,
            List<BranchCandidate> result)
        {
            _stepBuffer[depth] = step;
            int stepCount = depth + 1;

            var projection = _stateProjector.Project(snapshot, step);
            if (!projection.IsSafe || projection.NextState == null)
                return;

            var projectedSnapshot = projection.NextState.ToSnapshot();
            classifier.Classify(projectedSnapshot);

            result.Add(BuildCandidate(_stepBuffer, stepCount, originalSnapshot));

            if (stepCount >= MaxBranchDepth)
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
                    stepCount,
                    originalSnapshot,
                    result);
            }
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

            return new BranchCandidate
            {
                Steps = steps,
                Outcome = new BranchOutcome
                {
                    TotalEnergyCost = totalEnergyCost,
                    AllStepsSafe = IsIdlePeriodSafe(originalSnapshot, buffer[0])
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
