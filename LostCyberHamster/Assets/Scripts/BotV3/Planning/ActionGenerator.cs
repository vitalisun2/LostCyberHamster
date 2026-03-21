using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Генерирует возможные действия для видимых объектов.
    /// SwitchLane для same-lane Threats, Jump для малых препятствий.
    /// </summary>
    public class ActionGenerator
    {
        private const float SwitchLaneFireDist = 4.0f;
        internal const float SwitchLaneLatestSafeDist = 1.5f;

        private const float JumpFireDist = 1.5f;
        internal const int JumpEnergyCost = 10;

        /// <summary>Примерное расстояние, на которое хомяк улетает при Jump.</summary>
        internal const float JumpLandingOffset = 3.8f;
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
                if (!IsOnSameLane(snapshot, obs))
                    continue;
                if (!IsNearestSameLaneThreat(snapshot, obs))
                    continue;

                bool hasSwitchLane = TryBuildSwitchLaneStep(snapshot, obs, out BranchStep switchStep);
                bool hasJump = TryBuildJumpStep(snapshot, obs, out BranchStep jumpStep);

                if (hasSwitchLane)
                    result.Add(switchStep);
                if (hasJump)
                    result.Add(jumpStep);

                if (hasSwitchLane || hasJump)
                    BotLogger.LogActionCandidates(obs, hasSwitchLane, hasJump, snapshot);
            }

            return result;
        }

        private static bool TryBuildSwitchLaneStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            out BranchStep step)
        {
            step = null;

            if (threat.DistanceToHamster < SwitchLaneLatestSafeDist)
                return false;

            float executeAtDistance = SwitchLaneFireDist;
            if (executeAtDistance > threat.DistanceToHamster)
                executeAtDistance = threat.DistanceToHamster;
            if (executeAtDistance < SwitchLaneLatestSafeDist)
                executeAtDistance = SwitchLaneLatestSafeDist;

            if (!SwitchLaneSafety.IsSafeAtExecuteDistance(snapshot, threat, executeAtDistance))
                return false;

            step = new BranchStep(
                BotAction.SwitchLane,
                threat,
                executeAtDistance,
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

            if (!IsLandingClear(snapshot, snapshot.HamsterOnBottom, threat.StableId))
                return false;

            float executeAtDistance = JumpFireDist;
            if (executeAtDistance > threat.DistanceToHamster)
                executeAtDistance = threat.DistanceToHamster;

            step = new BranchStep(
                BotAction.Jump,
                threat,
                executeAtDistance,
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
        /// Проверяет, свободна ли зона приземления прыжка.
        /// </summary>
        private static bool IsLandingClear(BotSceneSnapshot snapshot, bool hamsterOnBottom, int excludeId)
        {
            float checkFrom = snapshot.HamsterRightX + JumpLandingOffset - JumpLandingMargin;
            float checkTo = snapshot.HamsterRightX + JumpLandingOffset + JumpLandingMargin;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == excludeId) continue;
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != hamsterOnBottom) continue;

                float absLeftX = snapshot.HamsterRightX + obs.DistanceToHamster;
                if (absLeftX >= checkFrom && absLeftX <= checkTo)
                    return false;
            }

            return true;
        }

        private static bool IsNearestSameLaneThreat(BotSceneSnapshot snapshot, ObstacleInfo threat)
        {
            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0f) continue;
                if (!IsOnSameLane(snapshot, obs)) continue;

                if (obs.DistanceToHamster < threat.DistanceToHamster)
                    return false;
            }

            return true;
        }

        internal static bool IsOnSameLane(BotSceneSnapshot snapshot, ObstacleInfo obstacle)
        {
            return snapshot.HamsterOnBottom == !obstacle.IsTopLane;
        }
    }
}
