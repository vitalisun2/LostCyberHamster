using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Стратегия построения и проекции SwitchLane через один ближайший safe fire shift.
    /// Planner не строит окна и timing-варианты: ему нужен только первый допустимый момент перестроения.
    /// </summary>
    public class SwitchLaneStrategy : IActionStrategy
    {
        private const float IntervalEpsilon = 0.001f;

        public BotAction Action => BotAction.SwitchLane;

        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            ProjectedWorld projectedWorld,
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

            if (!TryResolveSafeFireShift(
                    snapshot,
                    target,
                    out float fireWorldShift,
                    out rejectReason))
                return false;

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

        private static bool TryResolveSafeFireShift(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            out float fireWorldShift,
            out string rejectReason)
        {
            float sourceDeadlineShift = target.DistanceToHamster - BotPhysicsConsts.SafetyPadding;
            if (sourceDeadlineShift <= 0f)
            {
                fireWorldShift = 0f;
                rejectReason = "source deadline passed";
                return false;
            }

            if (!TryFindFirstSafeFireShift(snapshot, sourceDeadlineShift, out fireWorldShift))
            {
                rejectReason = "no safe fire shift";
                return false;
            }

            rejectReason = null;
            return true;
        }

        private static bool TryFindFirstSafeFireShift(
            BotSceneSnapshot snapshot,
            float sourceDeadlineShift,
            out float fireWorldShift)
        {
            var blockingIntervals = CollectBlockingIntervals(snapshot, sourceDeadlineShift);
            if (blockingIntervals.Count == 0)
            {
                fireWorldShift = 0f;
                return true;
            }

            blockingIntervals.Sort((a, b) => a.Start.CompareTo(b.Start));

            var firstInterval = blockingIntervals[0];
            if (firstInterval.Start > IntervalEpsilon)
            {
                fireWorldShift = 0f;
                return true;
            }

            float cursor = 0f;
            for (int i = 0; i < blockingIntervals.Count; i++)
            {
                var interval = blockingIntervals[i];
                if (interval.Start > cursor + IntervalEpsilon)
                    break;

                if (interval.End > cursor)
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

        private static List<BlockingInterval> CollectBlockingIntervals(
            BotSceneSnapshot snapshot,
            float sourceDeadlineShift)
        {
            var intervals = new List<BlockingInterval>();

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

                intervals.Add(new BlockingInterval(unsafeStart, unsafeEnd));
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

        private readonly struct BlockingInterval
        {
            public readonly float Start;
            public readonly float End;

            public BlockingInterval(float start, float end)
            {
                Start = start;
                End = end;
            }
        }
    }
}
