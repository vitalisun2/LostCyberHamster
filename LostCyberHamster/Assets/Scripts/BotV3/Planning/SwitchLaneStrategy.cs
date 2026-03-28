using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Стратегия построения и проекции SwitchLane через один ближайший safe fire moment.
    /// Planner не строит набор timing-вариантов: ему нужен первый допустимый момент перестроения.
    /// </summary>
    public class SwitchLaneStrategy
    {
        private const float IntervalEpsilon = 0.001f;

        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;

            if (problem == null || problem.Kind != ProblemKind.ThreatCollision)
            {
                rejectReason = "unsupported problem";
                return false;
            }

            var target = problem.SourceObstacle;

            if (!TryResolveFireWindow(
                    snapshot,
                    target,
                    out float earliestFireShift,
                    out float latestFireShift,
                    out rejectReason))
                return false;

            float fireWorldShift = earliestFireShift;
            float executeAtDistance = target.DistanceToHamster - fireWorldShift;
            if (executeAtDistance < 0f)
                executeAtDistance = 0f;

            float completionWorldShift = fireWorldShift + BotPhysicsConsts.SwitchLaneDecisionTravel;

            step = new BranchStep(
                BotAction.SwitchLane,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                energyCost: 0,
                $"SwitchLane avoid {target.Type}");

            step.SetFireWindow(earliestFireShift, latestFireShift);
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            var fireSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.FireWorldShift);
            if (!IsTargetLaneSweptSafe(fireSnapshot))
            {
                DebugManager.DiagLog(
                    $"[BotV3 PROJ] UNSAFE SwitchLane swept interval" +
                    $" → worldShift={step.FireWorldShift:F2}");

                return new StepProjectionResult
                {
                    IsSafe = false,
                    DebugReason = step.Reason
                };
            }

            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            completionSnapshot.HamsterOnBottom = !completionSnapshot.HamsterOnBottom;
            completionSnapshot.HamsterOnRoof = false;

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = PlannerState.FromSnapshot(completionSnapshot),
                DebugReason = step.Reason
            };
        }

        private static bool TryResolveFireWindow(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            out float earliestFireShift,
            out float latestFireShift,
            out string rejectReason)
        {
            float sourceDeadlineShift = target.DistanceToHamster - BotPhysicsConsts.SafetyPadding;
            if (sourceDeadlineShift <= 0f)
            {
                earliestFireShift = 0f;
                latestFireShift = 0f;
                rejectReason = "source deadline passed";
                return false;
            }

            if (!TryFindEarliestSafeFireShift(snapshot, sourceDeadlineShift, out earliestFireShift))
            {
                latestFireShift = 0f;
                rejectReason = "no safe fire window";
                return false;
            }

            // Для текущей модели planner-а сохраняем один канонический safe момент.
            latestFireShift = earliestFireShift;
            rejectReason = null;
            return true;
        }

        private static bool TryFindEarliestSafeFireShift(
            BotSceneSnapshot snapshot,
            float sourceDeadlineShift,
            out float fireWorldShift)
        {
            var unsafeIntervals = CollectUnsafeIntervals(snapshot, sourceDeadlineShift);
            if (unsafeIntervals.Count == 0)
            {
                fireWorldShift = 0f;
                return true;
            }

            unsafeIntervals.Sort((a, b) => a.Start.CompareTo(b.Start));

            float cursor = 0f;
            for (int i = 0; i < unsafeIntervals.Count; i++)
            {
                var interval = unsafeIntervals[i];
                if (interval.End <= cursor + IntervalEpsilon)
                    continue;

                if (interval.Start > cursor + IntervalEpsilon)
                {
                    fireWorldShift = cursor;
                    return true;
                }

                cursor = interval.End;
            }

            if (cursor <= sourceDeadlineShift - IntervalEpsilon)
            {
                fireWorldShift = cursor;
                return true;
            }

            fireWorldShift = 0f;
            return false;
        }

        private static List<UnsafeInterval> CollectUnsafeIntervals(
            BotSceneSnapshot snapshot,
            float sourceDeadlineShift)
        {
            var intervals = new List<UnsafeInterval>();

            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot) - BotPhysicsConsts.SafetyPadding;
            float hamsterRightX = snapshot.HamsterRightX + BotPhysicsConsts.SafetyPadding;
            bool targetLaneBottom = !snapshot.HamsterOnBottom;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetLaneBottom)
                    continue;

                float unsafeStart = obs.LeftX - BotPhysicsConsts.SwitchLaneDecisionTravel - hamsterRightX;
                float unsafeEnd = obs.RightX - hamsterLeftX;

                if (unsafeEnd <= 0f || unsafeStart >= sourceDeadlineShift)
                    continue;

                if (unsafeStart < 0f)
                    unsafeStart = 0f;

                if (unsafeEnd > sourceDeadlineShift)
                    unsafeEnd = sourceDeadlineShift;

                if (unsafeEnd - unsafeStart <= IntervalEpsilon)
                    continue;

                intervals.Add(new UnsafeInterval(unsafeStart, unsafeEnd));
            }

            return intervals;
        }

        private static bool IsTargetLaneSweptSafe(BotSceneSnapshot fireSnapshot)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(fireSnapshot) - BotPhysicsConsts.SafetyPadding;
            float hamsterRightX = fireSnapshot.HamsterRightX + BotPhysicsConsts.SafetyPadding;
            bool targetLaneBottom = !fireSnapshot.HamsterOnBottom;

            for (int i = 0; i < fireSnapshot.VisibleObjects.Count; i++)
            {
                var obs = fireSnapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetLaneBottom)
                    continue;

                float sweptLeftX = obs.LeftX - BotPhysicsConsts.SwitchLaneDecisionTravel;
                float sweptRightX = obs.RightX;

                if (CollisionUtils.IsOverlap(hamsterLeftX, hamsterRightX, sweptLeftX, sweptRightX))
                    return false;
            }

            return true;
        }

        private readonly struct UnsafeInterval
        {
            public readonly float Start;
            public readonly float End;

            public UnsafeInterval(float start, float end)
            {
                Start = start;
                End = end;
            }
        }
    }
}
