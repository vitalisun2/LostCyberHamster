using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Генерирует возможные действия для видимых объектов.
    /// V3 minimal: только SwitchLane для same-lane Threats.
    /// </summary>
    public class ActionGenerator
    {
        private const float SwitchLaneFireDist = 4.0f;
        internal const float SwitchLaneLatestSafeDist = 1.5f;

        public List<ChainStep> Generate(BotSceneSnapshot snapshot)
        {
            var result = new List<ChainStep>();

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

                if (TryBuildSwitchLaneStep(snapshot, obs, out ChainStep step))
                    result.Add(step);
            }

            return result;
        }

        private static bool TryBuildSwitchLaneStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            out ChainStep step)
        {
            step = null;

            if (threat.DistanceToHamster < SwitchLaneLatestSafeDist)
                return false;

            float executeAtDistance = SwitchLaneFireDist;
            if (executeAtDistance > threat.DistanceToHamster)
                executeAtDistance = threat.DistanceToHamster;
            if (executeAtDistance < SwitchLaneLatestSafeDist)
                executeAtDistance = SwitchLaneLatestSafeDist;

            if (!SwitchLaneSafety.IsImmediatelySafe(snapshot))
                return false;

            step = new ChainStep(
                BotAction.SwitchLane,
                threat,
                executeAtDistance,
                energyCost: 0,
                $"SwitchLane avoid {threat.Type}");

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
