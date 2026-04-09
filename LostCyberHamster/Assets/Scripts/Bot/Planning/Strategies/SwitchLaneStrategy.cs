using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия построения и проекции SwitchLane через детерминированный fire
    /// внутри последнего непрерывного safe-window.
    /// Безопасность считается одной и той же геометрией и при генерации кандидата,
    /// и при финальной проекции: во время всего интервала transition на целевой
    /// полосе не должно возникать overlap с угрозой.
    /// </summary>
    public class SwitchLaneStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.SwitchLane;

        /// <summary>
        /// Пробует построить шаг SwitchLane: валидация проблемы → поиск безопасного момента → создание шага.
        /// </summary>
        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;

            // Найти канонический safe fire moment для перестроения
            if (!TryFindCanonicalFireShift(
                snapshot,
                target,
                out float fireWorldShift,
                out float selectedWindowStart,
                out float selectedWindowEnd))
            {
                rejectReason = "no safe fire shift";
                return false;
            }

            BotLogger.LogSwitchLaneWindow(target, selectedWindowStart, selectedWindowEnd, fireWorldShift);

            // Создать шаг с рассчитанным таймингом
            float executeAtDistance = target.DistanceToHamster - fireWorldShift;
            if (executeAtDistance < 0f)
                executeAtDistance = 0f;

            float completionWorldShift = fireWorldShift + BotConsts.SwitchLaneDecisionTravel;

            step = new BranchStep(
                BotAction.SwitchLane,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                energyCost: 0,
                $"SwitchLane avoid {target.Type}");
            rejectReason = null;
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            bool targetLaneBottom = !snapshot.HamsterOnBottom;
            if (!TryGetBlockingObstacleDuringTransition(
                    snapshot,
                    targetLaneBottom,
                    step.FireWorldShift,
                    out var blockingObstacle,
                    out float unsafeStartShift,
                    out float unsafeEndShift))
            {
                BotLogger.LogSwitchLaneOverlap(step, blockingObstacle, unsafeStartShift, unsafeEndShift);
                return new StepProjectionResult
                {
                    IsSafe = false,
                    DebugReason = step.Reason
                };
            }

            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            completionSnapshot.HamsterOnBottom = !completionSnapshot.HamsterOnBottom;
            completionSnapshot.HamsterOnRoof = false;
            completionSnapshot.ActiveAvoidanceCommitments.Add(new AvoidanceCommitment(
                step.TargetObstacle.StableId,
                forbiddenLaneBottom: !step.TargetObstacle.IsTopLane));
            completionSnapshot.PruneInactiveAvoidanceCommitments();

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = PlannerState.FromSnapshot(completionSnapshot),
                DebugReason = step.Reason
            };
        }

        /// <summary>
        /// Ищет канонический fire moment для перестроения.
        /// Строит safe windows в диапазоне [release, deadline] по полной геометрии
        /// transition и выбирает точку внутри последнего непрерывного safe-window.
        /// </summary>
        private static bool TryFindCanonicalFireShift(
            BotSceneSnapshot snapshot,
            ObstacleInfo sourceTarget,
            out float fireWorldShift,
            out float selectedWindowStart,
            out float selectedWindowEnd)
        {
            bool targetLaneBottom = !snapshot.HamsterOnBottom;
            float releaseWorldShift = GetCommitmentReleaseShift(snapshot, targetLaneBottom);
            float deadlineWorldShift = sourceTarget.DistanceToHamster;

            if (releaseWorldShift > deadlineWorldShift)
            {
                fireWorldShift = 0f;
                selectedWindowStart = 0f;
                selectedWindowEnd = 0f;
                return false;
            }

            if (!TryFindLatestSafeWindow(
                    snapshot,
                    releaseWorldShift,
                    deadlineWorldShift,
                    targetLaneBottom,
                    out selectedWindowStart,
                    out selectedWindowEnd))
            {
                fireWorldShift = 0f;
                return false;
            }

            fireWorldShift = SelectFireShiftInsideWindow(selectedWindowStart, selectedWindowEnd);
            return true;
        }

        private static float GetCommitmentReleaseShift(BotSceneSnapshot snapshot, bool targetLaneBottom)
        {
            float releaseShift = 0f;

            for (int i = 0; i < snapshot.ActiveAvoidanceCommitments.Count; i++)
            {
                var commitment = snapshot.ActiveAvoidanceCommitments[i];
                if (!commitment.AppliesToTargetLane(targetLaneBottom))
                    continue;

                if (!commitment.TryGetReleaseWorldShift(snapshot, out float currentReleaseShift))
                    continue;

                if (currentReleaseShift > releaseShift)
                    releaseShift = currentReleaseShift;
            }

            return releaseShift;
        }

        private static bool TryFindLatestSafeWindow(
            BotSceneSnapshot snapshot,
            float safeWindowStart,
            float safeWindowEnd,
            bool targetLaneBottom,
            out float selectedWindowStart,
            out float selectedWindowEnd)
        {
            var unsafeIntervals = CollectUnsafeIntervals(
                snapshot,
                safeWindowStart,
                safeWindowEnd,
                targetLaneBottom);

            if (unsafeIntervals.Count == 0)
            {
                selectedWindowStart = safeWindowStart;
                selectedWindowEnd = safeWindowEnd;
                return true;
            }

            unsafeIntervals.Sort((a, b) => a.StartShift.CompareTo(b.StartShift));

            float currentSafeStart = safeWindowStart;
            bool hasSafeWindow = false;
            selectedWindowStart = 0f;
            selectedWindowEnd = 0f;

            for (int i = 0; i < unsafeIntervals.Count; i++)
            {
                var interval = unsafeIntervals[i];
                if (interval.EndShift <= currentSafeStart)
                    continue;

                if (interval.StartShift > currentSafeStart)
                {
                    selectedWindowStart = currentSafeStart;
                    selectedWindowEnd = interval.StartShift;
                    hasSafeWindow = true;
                }

                if (interval.EndShift > currentSafeStart)
                    currentSafeStart = interval.EndShift;
            }

            if (currentSafeStart <= safeWindowEnd)
            {
                selectedWindowStart = currentSafeStart;
                selectedWindowEnd = safeWindowEnd;
                hasSafeWindow = true;
            }

            return hasSafeWindow;
        }

        private static List<UnsafeFireInterval> CollectUnsafeIntervals(
            BotSceneSnapshot snapshot,
            float safeWindowStart,
            float safeWindowEnd,
            bool targetLaneBottom)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;
            var unsafeIntervals = new List<UnsafeFireInterval>();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetLaneBottom)
                    continue;

                float unsafeStart = obs.LeftX - hamsterRightX - BotConsts.SwitchLaneDecisionTravel;
                float unsafeEnd = obs.RightX - hamsterLeftX;

                if (unsafeEnd <= safeWindowStart || unsafeStart >= safeWindowEnd)
                    continue;

                if (unsafeStart < safeWindowStart)
                    unsafeStart = safeWindowStart;

                if (unsafeEnd > safeWindowEnd)
                    unsafeEnd = safeWindowEnd;

                unsafeIntervals.Add(new UnsafeFireInterval(unsafeStart, unsafeEnd));
            }

            return unsafeIntervals;
        }

        private static float SelectFireShiftInsideWindow(float windowStart, float windowEnd)
        {
            if (windowEnd <= windowStart)
                return windowStart;

            float ratio = BotConsts.SwitchLaneWindowSelectionRatio;
            if (ratio < 0f)
                ratio = 0f;
            else if (ratio > 1f)
                ratio = 1f;

            return windowStart + (windowEnd - windowStart) * ratio;
        }

        /// <summary>
        /// Проверяет, что в течение всего SwitchLane-transition на целевой полосе
        /// нет overlap с угрозой. Возвращает первое блокирующее obstacle и unsafe
        /// fire interval, чтобы builder и projector опирались на одну геометрию.
        /// </summary>
        private static bool TryGetBlockingObstacleDuringTransition(
            BotSceneSnapshot snapshot,
            bool targetLaneBottom,
            float fireWorldShift,
            out ObstacleInfo blockingObstacle,
            out float unsafeStartShift,
            out float unsafeEndShift)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;
            float transitionEndShift = fireWorldShift + BotConsts.SwitchLaneDecisionTravel;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetLaneBottom)
                    continue;

                float obstacleOverlapStartShift = obs.LeftX - hamsterRightX;
                float obstacleOverlapEndShift = obs.RightX - hamsterLeftX;

                if (transitionEndShift <= obstacleOverlapStartShift)
                    continue;

                if (fireWorldShift >= obstacleOverlapEndShift)
                    continue;

                unsafeStartShift = obstacleOverlapStartShift - BotConsts.SwitchLaneDecisionTravel;
                unsafeEndShift = obstacleOverlapEndShift;
                blockingObstacle = obs;
                return false;
            }

            blockingObstacle = default;
            unsafeStartShift = 0f;
            unsafeEndShift = 0f;
            return true;
        }

        private readonly struct UnsafeFireInterval
        {
            public UnsafeFireInterval(float startShift, float endShift)
            {
                StartShift = startShift;
                EndShift = endShift;
            }

            public float StartShift { get; }
            public float EndShift { get; }
        }
    }
}
