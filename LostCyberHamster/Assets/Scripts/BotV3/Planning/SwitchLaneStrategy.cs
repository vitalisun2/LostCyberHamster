using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.BotV3
{
    public enum SwitchLaneTimingMode
    {
        Earliest,
        Latest
    }

    /// <summary>
    /// Стратегия построения и проекции SwitchLane через окно допустимого fire.
    /// Стратегия одна, а timing-вариант передаётся параметром при построении кандидата.
    /// </summary>
    public class SwitchLaneStrategy
    {
        private const float IntervalEpsilon = 0.001f;

        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            SwitchLaneTimingMode timingMode,
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
                    timingMode,
                    out float earliestFireShift,
                    out float latestFireShift,
                    out rejectReason))
                return false;

            float fireWorldShift = ChooseCanonicalFireShift(
                earliestFireShift,
                latestFireShift,
                timingMode);
            if (!float.IsFinite(fireWorldShift))
            {
                rejectReason = "timing variant collapsed";
                return false;
            }

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

        private static float ChooseCanonicalFireShift(
            float earliestFireShift,
            float latestFireShift,
            SwitchLaneTimingMode timingMode)
        {
            switch (timingMode)
            {
                case SwitchLaneTimingMode.Earliest:
                    return earliestFireShift;

                case SwitchLaneTimingMode.Latest:
                    if (latestFireShift <= earliestFireShift + IntervalEpsilon)
                        return float.NaN;

                    return latestFireShift - IntervalEpsilon;

                default:
                    return earliestFireShift;
            }
        }

        private bool TryResolveFireWindow(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            SwitchLaneTimingMode timingMode,
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

            var safeWindows = CollectSafeWindows(snapshot, sourceDeadlineShift);
            if (safeWindows.Count == 0)
            {
                earliestFireShift = 0f;
                latestFireShift = 0f;
                rejectReason = "no safe fire window";
                return false;
            }

            var selectedWindow = timingMode == SwitchLaneTimingMode.Earliest
                ? safeWindows[0]
                : safeWindows[safeWindows.Count - 1];

            earliestFireShift = selectedWindow.Start;
            latestFireShift = selectedWindow.End;
            rejectReason = null;
            return true;
        }

        private static List<SafeWindow> CollectSafeWindows(
            BotSceneSnapshot snapshot,
            float sourceDeadlineShift)
        {
            var windows = new List<SafeWindow>();
            var unsafeIntervals = CollectUnsafeIntervals(snapshot, sourceDeadlineShift);
            if (unsafeIntervals.Count == 0)
            {
                windows.Add(new SafeWindow(0f, sourceDeadlineShift));
                return windows;
            }

            unsafeIntervals.Sort((a, b) => a.Start.CompareTo(b.Start));

            float cursor = 0f;
            for (int i = 0; i < unsafeIntervals.Count; i++)
            {
                var interval = unsafeIntervals[i];
                if (interval.End <= cursor + IntervalEpsilon)
                    continue;

                if (interval.Start > cursor + IntervalEpsilon)
                    windows.Add(new SafeWindow(cursor, interval.Start));

                cursor = interval.End;
            }

            if (cursor <= sourceDeadlineShift - IntervalEpsilon)
                windows.Add(new SafeWindow(cursor, sourceDeadlineShift));

            return windows;
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

        private readonly struct SafeWindow
        {
            public readonly float Start;
            public readonly float End;

            public SafeWindow(float start, float end)
            {
                Start = start;
                End = end;
            }
        }
    }
}
