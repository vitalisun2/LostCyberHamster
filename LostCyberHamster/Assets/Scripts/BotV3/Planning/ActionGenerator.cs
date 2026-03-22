using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Генерирует возможные действия для видимых объектов.
    /// SwitchLane для same-lane Threats, Jump для малых препятствий.
    /// Каждый шаг включает FireWorldShift и CompletionWorldShift.
    /// </summary>
    public class ActionGenerator
    {
        private const float SwitchLaneFireDist = 4.0f;
        private const float SwitchLaneMinDist = 1.5f;

        private const float JumpFireDist = 1.5f;
        internal const int JumpEnergyCost = 10;

        private const float JumpLandingMargin = 1.2f;

        public List<BranchStep> Generate(BotSceneSnapshot snapshot)
        {
            var result = new List<BranchStep>();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.Category != ObjectCategory.Threat)
                    continue;
                if (obs.DistanceToHamster < 0f)
                    continue;
                if (!snapshot.IsOnSameLane(obs))
                    continue;
                if (!IsNearestSameLaneThreat(snapshot, obs))
                    continue;

                bool hasSwitchLane = TryBuildSwitchLaneStep(snapshot, obs, out BranchStep switchStep, out string switchRejectReason);
                bool hasJump = TryBuildJumpStep(snapshot, obs, out BranchStep jumpStep);

                if (hasSwitchLane)
                    result.Add(switchStep);
                if (hasJump)
                    result.Add(jumpStep);

                if (hasSwitchLane || hasJump)
                    BotLogger.LogActionCandidates(obs, hasSwitchLane, hasJump, switchRejectReason, snapshot);
            }

            return result;
        }

        private static bool TryBuildSwitchLaneStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;
            rejectReason = null;

            if (threat.DistanceToHamster < SwitchLaneMinDist)
            {
                rejectReason = "too close";
                return false;
            }

            float executeAtDistance = SwitchLaneFireDist;
            if (executeAtDistance > threat.DistanceToHamster)
                executeAtDistance = threat.DistanceToHamster;
            if (executeAtDistance < SwitchLaneMinDist)
                executeAtDistance = SwitchLaneMinDist;

            // Найти минимальный worldShift, при котором target lane свободна от overlap
            float minClearanceShift = GetTargetLaneClearanceShift(snapshot);
            float fireWorldShift = threat.DistanceToHamster - executeAtDistance;

            if (fireWorldShift < minClearanceShift)
            {
                // Сдвигаем fire позже — ждём пока obstacle проедет
                fireWorldShift = minClearanceShift;
                executeAtDistance = threat.DistanceToHamster - fireWorldShift;

                if (executeAtDistance < SwitchLaneMinDist)
                {
                    rejectReason = "target lane blocked until too close";
                    return false;
                }
            }

            float completionWorldShift = fireWorldShift + BotPhysicsConsts.SwitchLaneFullTravel;

            step = new BranchStep(
                BotAction.SwitchLane,
                threat,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                energyCost: 0,
                $"SwitchLane avoid {threat.Type}");

            return true;
        }

        private static bool TryBuildJumpStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            out BranchStep step)
        {
            step = null;

            if (!IsSmallObstacle(threat.Type))
                return false;

            if (snapshot.Energy < JumpEnergyCost)
                return false;

            float executeAtDistance = JumpFireDist;
            if (executeAtDistance > threat.DistanceToHamster)
                executeAtDistance = threat.DistanceToHamster;

            float fireWorldShift = threat.DistanceToHamster - executeAtDistance;
            float completionWorldShift = fireWorldShift + BotPhysicsConsts.JumpLandingOffset;

            // Проверяем зону приземления в спроецированном мире
            if (!IsLandingClear(snapshot, snapshot.HamsterOnBottom, threat.StableId, completionWorldShift))
                return false;

            step = new BranchStep(
                BotAction.Jump,
                threat,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                JumpEnergyCost,
                $"Jump over {threat.Type}");

            return true;
        }

        private static bool IsSmallObstacle(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.smallNotAliveRoad
                || type == ObstacleTypeEnum.smallNotAliveRoadAndRoof;
        }

        /// <summary>
        /// Проверяет, свободна ли зона приземления прыжка в спроецированном мире.
        /// </summary>
        private static bool IsLandingClear(
            BotSceneSnapshot snapshot,
            bool hamsterOnBottom,
            int excludeId,
            float completionWorldShift)
        {
            float hamsterRightX = snapshot.HamsterRightX;
            float hamsterLeftX = hamsterRightX - snapshot.HamsterWidth;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == excludeId) continue;
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != hamsterOnBottom) continue;

                float shiftedLeftX = obs.LeftX - completionWorldShift;
                float shiftedRightX = obs.RightX - completionWorldShift;

                if (Common.CollisionUtils.IsOverlap(
                    hamsterLeftX - BotPhysicsConsts.SafetyPadding,
                    hamsterRightX + BotPhysicsConsts.SafetyPadding,
                    shiftedLeftX, shiftedRightX))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Определяет, на сколько мир должен сдвинуться, чтобы target lane
        /// была свободна от overlap с хомяком по X.
        /// Возвращает 0, если target lane уже свободна.
        /// </summary>
        private static float GetTargetLaneClearanceShift(BotSceneSnapshot snapshot)
        {
            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            float hamsterRightX = snapshot.HamsterRightX;
            bool targetIsBottom = !snapshot.HamsterOnBottom;
            float maxShift = 0f;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.Category != ObjectCategory.Threat) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetIsBottom) continue;

                // Overlap пропадёт когда obs.RightX - shift < hamsterLeftX
                // shift > obs.RightX - hamsterLeftX
                if (obs.RightX > hamsterLeftX && obs.LeftX < hamsterRightX)
                {
                    float needed = obs.RightX - hamsterLeftX;
                    if (needed > maxShift)
                        maxShift = needed;
                }
            }

            return maxShift;
        }

        private static bool IsNearestSameLaneThreat(BotSceneSnapshot snapshot, ObstacleInfo threat)
        {
            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0f) continue;
                if (!snapshot.IsOnSameLane(obs)) continue;

                if (obs.DistanceToHamster < threat.DistanceToHamster)
                    return false;
            }

            return true;
        }
    }
}
