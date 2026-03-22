using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Проецирует состояние мира на две контрольные точки шага:
    ///   1. Fire moment — мир сдвинулся на FireWorldShift, проверяем безопасность начала шага.
    ///   2. Completion moment — мир сдвинулся на CompletionWorldShift, проверяем результат шага.
    ///
    /// Хомяк стоит на месте по X. Obstacles сдвигаются влево на worldShift.
    /// </summary>
    public class StateProjector
    {
        private const float PassedObstacleMargin = 0.4f;

        /// <summary>
        /// Проецирует шаг: проверяет safety на fire и completion, возвращает completion state.
        /// </summary>
        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            // 1. Fire safety: проецируем snapshot на момент fire, проверяем target lane
            var fireSnapshot = ProjectSnapshot(snapshot, step.FireWorldShift);
            bool fireIsSafe = IsSafeAtFire(fireSnapshot, step);

            if (!fireIsSafe)
            {
                LogUnsafe("FIRE", fireSnapshot, step, step.FireWorldShift);
                return new StepProjectionResult { IsSafe = false, DebugReason = step.Reason };
            }

            // 2. Completion: проецируем snapshot на момент завершения шага
            var completionSnapshot = ProjectSnapshot(snapshot, step.CompletionWorldShift);
            ApplyStepEffects(completionSnapshot, step);

            bool completionIsSafe = IsSafeAtCompletion(completionSnapshot, step);

            if (!completionIsSafe)
            {
                LogUnsafe("COMPLETION", completionSnapshot, step, step.CompletionWorldShift);
                return new StepProjectionResult { IsSafe = false, DebugReason = step.Reason };
            }

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = PlannerState.FromSnapshot(completionSnapshot),
                DebugReason = step.Reason
            };
        }

        /// <summary>
        /// Проецирует snapshot: сдвигает все obstacles влево на worldShift,
        /// убирает уехавшие за хомяка, пересчитывает distance.
        /// Хомяк остаётся на месте.
        /// </summary>
        private static BotSceneSnapshot ProjectSnapshot(BotSceneSnapshot source, float worldShift)
        {
            var projected = new BotSceneSnapshot();
            projected.CopyFrom(source);
            projected.SnapshotTime = source.SnapshotTime;

            float hamsterRightX = source.HamsterRightX;

            for (int i = 0; i < source.VisibleObjects.Count; i++)
            {
                var obs = source.VisibleObjects[i];

                float newLeftX = obs.LeftX - worldShift;
                float newRightX = obs.RightX - worldShift;
                float newCenterX = obs.CenterX - worldShift;

                if (newRightX < hamsterRightX - source.HamsterWidth - PassedObstacleMargin)
                    continue;

                float newDistance = newLeftX - hamsterRightX;

                projected.VisibleObjects.Add(new ObstacleInfo(
                    obs.Type, obs.IsTopLane,
                    newLeftX, newRightX, newCenterX,
                    newDistance, obs.Category, obs.StableId));
            }

            return projected;
        }

        /// <summary>
        /// Применяет эффекты шага к snapshot (меняет lane, energy).
        /// Не меняет позиции — они уже спроецированы.
        /// </summary>
        private static void ApplyStepEffects(BotSceneSnapshot snapshot, BranchStep step)
        {
            switch (step.Action)
            {
                case BotAction.SwitchLane:
                    snapshot.HamsterOnBottom = !snapshot.HamsterOnBottom;
                    snapshot.HamsterOnRoof = false;
                    break;

                case BotAction.Jump:
                    snapshot.HamsterOnRoof = false;
                    snapshot.Energy -= step.EnergyCost;
                    if (snapshot.Energy < 0) snapshot.Energy = 0;
                    // Убираем target obstacle (перепрыгнули)
                    snapshot.VisibleObjects.RemoveAll(o => o.StableId == step.TargetObstacle.StableId);
                    break;
            }
        }

        /// <summary>
        /// Безопасно ли начать шаг в этот момент?
        /// SwitchLane: нет overlap на target lane по X с хомяком.
        /// Jump: always safe at fire (проверка приземления — в completion).
        /// </summary>
        /// <summary>
        /// Безопасно ли начать SwitchLane в этот момент?
        /// Проецированный snapshot на момент fire: проверяем overlap
        /// хомяка с каждым target-lane threat.
        /// </summary>
        private static bool IsSafeAtFire(BotSceneSnapshot snapshot, BranchStep step)
        {
            if (step.Action != BotAction.SwitchLane)
                return true;

            bool targetIsBottom = !snapshot.HamsterOnBottom;
            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!IsThreatType(obs.Type)) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetIsBottom) continue;

                if (CollisionUtils.IsOverlap(hamsterLeftX, hamsterRightX, obs.LeftX, obs.RightX))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Безопасен ли результат шага?
        /// Проверяет: на lane хомяка (после применения эффектов) нет overlap с threats.
        /// </summary>
        private static bool IsSafeAtCompletion(BotSceneSnapshot snapshot, BranchStep step)
        {
            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == step.TargetObstacle.StableId) continue;
                if (!IsThreatType(obs.Type)) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != snapshot.HamsterOnBottom) continue;

                if (CollisionUtils.IsOverlap(
                    hamsterLeftX - BotPhysicsConsts.SafetyPadding,
                    hamsterRightX + BotPhysicsConsts.SafetyPadding,
                    obs.LeftX, obs.RightX))
                    return false;
            }

            return true;
        }

        private static bool IsThreatType(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallAlive:
                    return true;
                default:
                    return false;
            }
        }

        private static void LogUnsafe(string phase, BotSceneSnapshot snapshot, BranchStep step, float worldShift)
        {
            string lane = snapshot.HamsterOnBottom ? "bottom" : "top";
            if (phase == "COMPLETION" && step.Action == BotAction.SwitchLane)
                lane = !snapshot.HamsterOnBottom ? "bottom" : "top";

            DebugManager.DiagLog(
                $"[BotV3 PROJ] UNSAFE {step.Action} at {phase}" +
                $" → targetLane={lane} worldShift={worldShift:F2}");
        }
    }
}
