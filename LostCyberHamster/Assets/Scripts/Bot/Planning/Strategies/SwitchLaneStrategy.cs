using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия построения и проекции SwitchLane через детерминированную точку
    /// внутри последнего непрерывного safe-window до дедлайна текущей угрозы.
    /// Планировщик не жмёт earliest или latest, а выбирает mid-safe fire внутри
    /// самого позднего окна, которое ещё остаётся безопасным.
    /// Project() только вычисляет состояние мира после завершения шага.
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
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            completionSnapshot.HamsterOnBottom = !completionSnapshot.HamsterOnBottom;
            completionSnapshot.HamsterOnRoof = false;
            completionSnapshot.ActiveAvoidanceCommitments.Add(new AvoidanceCommitment(
                step.TargetObstacle.StableId,
                forbiddenLaneBottom: !step.TargetObstacle.IsTopLane));
            completionSnapshot.PruneInactiveAvoidanceCommitments();

            if (!TryGetBlockingObstacleAtCompletion(completionSnapshot, out var blockingObstacle))
            {
                BotLogger.LogSwitchLaneOverlap(step, blockingObstacle);
                return new StepProjectionResult
                {
                    IsSafe = false,
                    DebugReason = step.Reason
                };
            }

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = PlannerState.FromSnapshot(completionSnapshot),
                DebugReason = step.Reason
            };
        }

        /// <summary>
        /// Ищет канонический fire moment для перестроения.
        /// Сначала строит safe windows в диапазоне [release, deadline], затем выбирает
        /// середину последнего непрерывного safe-window. Это даёт боту больше информации,
        /// чем earliest-safe, но выглядит спокойнее, чем latest-safe.
        /// </summary>
        private static bool TryFindCanonicalFireShift(
            BotSceneSnapshot snapshot,
            ObstacleInfo sourceTarget,
            out float fireWorldShift,
            out float selectedWindowStart,
            out float selectedWindowEnd)
        {
            bool targetLaneBottom = !snapshot.HamsterOnBottom;
            float safeWindowStart = GetCommitmentReleaseShift(snapshot, targetLaneBottom);
            float safeWindowEnd = sourceTarget.DistanceToHamster;

            if (safeWindowStart > safeWindowEnd)
            {
                fireWorldShift = 0f;
                selectedWindowStart = 0f;
                selectedWindowEnd = 0f;
                return false;
            }

            if (!TryFindLatestSafeWindow(snapshot, sourceTarget, safeWindowStart, safeWindowEnd,
                    targetLaneBottom, out selectedWindowStart, out selectedWindowEnd))
            {
                fireWorldShift = 0f;
                selectedWindowStart = 0f;
                selectedWindowEnd = 0f;
                return false;
            }

            fireWorldShift = SelectFireShiftInsideWindow(selectedWindowStart, selectedWindowEnd);
            return true;
        }

        private static bool TryFindLatestSafeWindow(
            BotSceneSnapshot snapshot,
            ObstacleInfo sourceTarget,
            float safeWindowStart,
            float safeWindowEnd,
            bool targetLaneBottom,
            out float selectedWindowStart,
            out float selectedWindowEnd)
        {
            var unsafeIntervals = CollectUnsafeIntervals(
                snapshot,
                sourceTarget,
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
            ObstacleInfo sourceTarget,
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

                if (obs.LeftX >= sourceTarget.LeftX)
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

        /// <summary>
        /// Проверяет, что на целевой полосе нет коллизий с хомяком в момент завершения перехода.
        /// </summary>
        private static bool TryGetBlockingObstacleAtCompletion(
            BotSceneSnapshot snapshot,
            out ObstacleInfo blockingObstacle)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != snapshot.HamsterOnBottom)
                    continue;

                if (CollisionUtils.IsOverlap(hamsterLeftX, hamsterRightX, obs.LeftX, obs.RightX))
                {
                    blockingObstacle = obs;
                    return false;
                }
            }

            blockingObstacle = default;
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
