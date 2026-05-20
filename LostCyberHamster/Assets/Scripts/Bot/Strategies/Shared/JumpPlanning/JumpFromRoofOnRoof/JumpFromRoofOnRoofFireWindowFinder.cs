using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof
{
    /// <summary>
    /// Подбирает fire shift для прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofFireWindowFinder
    {
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        public JumpFromRoofOnRoofFireWindowFinder(IJumpFromRoofOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift, подтвержденный runtime roof-jump resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpFromRoofOnRoofTravel travel,
            out ObstacleSnapshot targetRoof,
            out int targetRoofIndex,
            out float fireShift)
        {
            // Инициализирует пустой результат.
            targetRoof = null;
            targetRoofIndex = -1;
            fireShift = 0f;

            // Находит target roof для текущего roof-to-roof сценария.
            if (!TryFindTargetRoof(
                    planningState,
                    projectedWorldSnapshot,
                    chain,
                    travel,
                    out ObstacleSnapshot lastRoof,
                    out ObstacleSnapshot runFromRoofBlocker,
                    out ObstacleSnapshot lastObstacleBeforeTargetRoof,
                    out targetRoof,
                    out targetRoofIndex))
            {
                LogReject(planningState, "targetRoof", lastRoof, targetRoof);
                return false;
            }

            // Вычисляет геометрическое окно запуска.
            if (!JumpFromRoofOnRoofWindowCalculator.TryCalculate(
                    planningState,
                    lastRoof,
                    targetRoof,
                    runFromRoofBlocker,
                    lastObstacleBeforeTargetRoof,
                    _policy.BigAliveCollisionPaddingRatio,
                    travel,
                    out float firstFireShift,
                    out float lastFireShift,
                    out fireShift))
            {
                LogWindowReject(
                    planningState,
                    lastRoof,
                    targetRoof,
                    runFromRoofBlocker,
                    lastObstacleBeforeTargetRoof,
                    firstFireShift,
                    lastFireShift);
                LogReject(planningState, "window", lastRoof, targetRoof);
                return false;
            }

            // Подтверждает выбранную точку через runtime-equivalent resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            bool runtimeOutcomeMatches = CheckRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                targetRoof.InstanceId,
                fireShift,
                travel);
            if (!runtimeOutcomeMatches)
            {
                LogReject(planningState, "runtimeOutcome", lastRoof, targetRoof, fireShift);
            }

            return runtimeOutcomeMatches;
        }

        /// <summary>
        /// Находит следующую roof-цель, если простой сход с крыши опасен для текущего decision point.
        /// </summary>
        internal bool TryFindTargetRoof(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpFromRoofOnRoofTravel travel,
            out ObstacleSnapshot lastRoof,
            out ObstacleSnapshot runFromRoofBlocker,
            out ObstacleSnapshot lastObstacleBeforeTargetRoof,
            out ObstacleSnapshot targetRoof,
            out int targetRoofIndex)
        {
            lastRoof = null;
            runFromRoofBlocker = null;
            lastObstacleBeforeTargetRoof = null;
            targetRoof = null;
            targetRoofIndex = -1;

            if (planningState == null || projectedWorldSnapshot == null || chain == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out lastRoof,
                    out int lastRoofIndex))
            {
                LogSupportLookupFailure(planningState, projectedWorldSnapshot);
                return false;
            }

            // Одним проходом подтверждает blocker для схода с крыши и находит следующую roof-цель.
            bool hasRunFromRoofBlocker = false;
            ObstacleSnapshot firstRoofAhead = null;
            ObstacleSnapshot firstDamageAhead = null;
            for (int obstacleIndex = lastRoofIndex + 1;
                 obstacleIndex < projectedWorldSnapshot.Obstacles.Count;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (obstacle.RightX <= lastRoof.RightX)
                    continue;

                if (targetRoof == null && ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                {
                    targetRoof = obstacle;
                    targetRoofIndex = obstacleIndex;
                    firstRoofAhead = obstacle;

                    if (hasRunFromRoofBlocker)
                        return true;
                }

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                firstDamageAhead ??= obstacle;
                if (targetRoof == null)
                    lastObstacleBeforeTargetRoof = obstacle;

                float gap = obstacle.LeftX - lastRoof.RightX;
                if (gap >= travel.RunFromRoofTravel && !hasRunFromRoofBlocker)
                {
                    LogTargetSearchMiss(planningState, lastRoof, firstRoofAhead, firstDamageAhead, "blockerTooFar", gap, travel.RunFromRoofTravel);
                    return false;
                }

                if (!chain.ContainsObstacle(obstacle) && !hasRunFromRoofBlocker)
                {
                    LogTargetSearchMiss(planningState, lastRoof, firstRoofAhead, firstDamageAhead, "blockerOutsideChain", gap, travel.RunFromRoofTravel);
                    return false;
                }

                hasRunFromRoofBlocker = true;
                runFromRoofBlocker ??= obstacle;
                if (targetRoof != null)
                    return true;
            }

            LogTargetSearchMiss(
                planningState,
                lastRoof,
                firstRoofAhead,
                firstDamageAhead,
                hasRunFromRoofBlocker ? "noTargetRoof" : "noBlocker",
                0f,
                travel.RunFromRoofTravel);
            return false;
        }

        /// <summary>
        /// Проверяет runtime outcome для указанного fire shift.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int expectedTargetRoofInstanceId,
            float fireShift,
            JumpFromRoofOnRoofTravel travel)
        {
            if (planningState == null || projectedWorldSnapshot == null || baseObstacles == null)
                return false;

            // Строит obstacle snapshot на момент fire.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Готовит roof-jump context из текущей геометрии хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.RoofJumpTravel,
                travel.JumpFromRoofTravel);

            // Сверяет resolver outcome с ожидаемой посадкой на конкретную target roof.
            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            LogRuntimeOutcome(
                planningState,
                obstaclesAtFireShift,
                expectedTargetRoofInstanceId,
                fireShift,
                result);

            if (result.State != _policy.ExpectedSuccessState)
                return false;

            if (result.TargetIndex < 0 || result.TargetIndex >= obstaclesAtFireShift.Count)
                return false;

            return obstaclesAtFireShift[result.TargetIndex].InstanceId == expectedTargetRoofInstanceId
                && result.TargetIndex < projectedWorldSnapshot.Obstacles.Count
                && projectedWorldSnapshot.Obstacles[result.TargetIndex].InstanceId == expectedTargetRoofInstanceId;
        }

        private void LogReject(
            PlanningState planningState,
            string reason,
            ObstacleSnapshot lastRoof = null,
            ObstacleSnapshot targetRoof = null,
            float fireShift = 0f)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null)
                return;

            DebugManager.DiagLog(
                $"[{_policy.ActionKind} FIND] REJECT reason={reason} " +
                $"state={hamster.HamsterState} energy={hamster.Energy} " +
                $"projection={planningState.ProjectionWorldShift:F3} fireShift={fireShift:F3} " +
                $"lastRoof={FormatObstacle(lastRoof)} targetRoof={FormatObstacle(targetRoof)}");
        }

        private void LogRuntimeOutcome(
            PlanningState planningState,
            IReadOnlyList<JumpObstacleData> obstaclesAtFireShift,
            int expectedTargetRoofInstanceId,
            float fireShift,
            JumpResolveResult result)
        {
            if (planningState?.Hamster == null)
                return;

            string resultTarget = result.TargetIndex >= 0 && result.TargetIndex < obstaclesAtFireShift.Count
                ? $"{obstaclesAtFireShift[result.TargetIndex].Type}/{obstaclesAtFireShift[result.TargetIndex].InstanceId}"
                : "none";

            DebugManager.DiagLog(
                $"[{_policy.ActionKind} FIND] OUTCOME fireShift={fireShift:F3} " +
                $"state={result.State} targetIndex={result.TargetIndex} target={resultTarget} " +
                $"expectedState={_policy.ExpectedSuccessState} expectedTarget={expectedTargetRoofInstanceId}");
        }

        private void LogWindowReject(
            PlanningState planningState,
            ObstacleSnapshot lastRoof,
            ObstacleSnapshot targetRoof,
            ObstacleSnapshot runFromRoofBlocker,
            ObstacleSnapshot lastObstacleBeforeTargetRoof,
            float firstFireShift,
            float lastFireShift)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null)
                return;

            float padding = hamster.Width * _policy.BigAliveCollisionPaddingRatio;
            DebugManager.DiagLog(
                $"[{_policy.ActionKind} FIND] WINDOW_REJECT " +
                $"first={firstFireShift:F3} last={lastFireShift:F3} padding={padding:F3} " +
                $"state={hamster.HamsterState} energy={hamster.Energy} " +
                $"projection={planningState.ProjectionWorldShift:F3} " +
                $"lastRoof={FormatObstacle(lastRoof)} targetRoof={FormatObstacle(targetRoof)} " +
                $"firstObstacle={FormatObstacle(runFromRoofBlocker)} " +
                $"lastObstacle={FormatObstacle(lastObstacleBeforeTargetRoof)}");
        }

        private static string FormatObstacle(ObstacleSnapshot obstacle)
        {
            if (obstacle == null)
                return "none";

            return $"{obstacle.ObstacleType}/{obstacle.InstanceId}/[{obstacle.LeftX:F2},{obstacle.RightX:F2}]";
        }

        private void LogSupportLookupFailure(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null || projectedWorldSnapshot == null)
                return;

            int supportId = hamster.RoofSupportInstanceId ?? -1;
            string supportCandidate = "none";
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.InstanceId != supportId)
                    continue;

                supportCandidate =
                    $"{obstacle.ObstacleType}/{obstacle.InstanceId}/" +
                    $"lane={(obstacle.IsBottomLine ? "bottom" : "top")}/" +
                    $"[{obstacle.LeftX:F2},{obstacle.RightX:F2}]";
                break;
            }

            DebugManager.DiagLog(
                $"[{_policy.ActionKind} FIND] SUPPORT_MISS supportId={supportId} " +
                $"hamsterLane={(hamster.IsOnBottomLine ? "bottom" : "top")} " +
                $"candidate={supportCandidate} projection={planningState.ProjectionWorldShift:F3}");
        }

        private void LogTargetSearchMiss(
            PlanningState planningState,
            ObstacleSnapshot lastRoof,
            ObstacleSnapshot firstRoofAhead,
            ObstacleSnapshot firstDamageAhead,
            string detail,
            float gap,
            float runFromRoofTravel)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null)
                return;

            DebugManager.DiagLog(
                $"[{_policy.ActionKind} FIND] TARGET_MISS detail={detail} " +
                $"state={hamster.HamsterState} energy={hamster.Energy} " +
                $"projection={planningState.ProjectionWorldShift:F3} " +
                $"lastRoof={FormatObstacle(lastRoof)} " +
                $"firstRoof={FormatObstacle(firstRoofAhead)} " +
                $"firstDamage={FormatObstacle(firstDamageAhead)} " +
                $"gap={gap:F3} runTravel={runFromRoofTravel:F3}");
        }

    }
}
