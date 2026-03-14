using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Генерирует список безопасных действий для ближайшей угрозы на линии хомяка.
    /// Этап 1: только SwitchLane и Jump для smallNotAliveRoad.
    /// Работает только со snapshot-данными.
    /// </summary>
    public class ActionGenerator
    {
        private const float SwitchLaneFireDist = 4.0f;
        private const float JumpFireDist       = 1.5f;

        /// <summary>Примерное расстояние, на которое хомяк улетает при Jump.</summary>
        private const float JumpLandingOffset = 3.8f;
        private const float JumpLandingMargin = 1.2f;

        public List<ChainStep> Generate(BotSceneSnapshot snapshot)
        {
            var result = new List<ChainStep>();

            // Ищем ближайшую угрозу на линии хомяка
            ObstacleInfo? nearest = FindNearestThreatOnHamsterLane(snapshot);
            if (nearest == null) return result;

            var threat = nearest.Value;

            // Вариант 1: SwitchLane — если другая линия свободна от угроз
            bool otherLaneSafe = IsOtherLaneSafe(snapshot, !snapshot.HamsterOnBottom, threat.StableId);
            if (otherLaneSafe)
            {
                result.Add(new ChainStep(
                    BotAction.SwitchLane, threat,
                    SwitchLaneFireDist, energyCost: 0,
                    "SwitchLane away from threat"));
            }

            // Вариант 2: Jump — если хватает энергии и зона приземления свободна
            if (snapshot.Energy >= 10)
            {
                bool landingClear = IsLandingClear(snapshot, snapshot.HamsterOnBottom, threat.StableId);
                if (landingClear)
                {
                    result.Add(new ChainStep(
                        BotAction.Jump, threat,
                        JumpFireDist, energyCost: 10,
                        "Jump over threat"));
                }
            }

            return result;
        }

        private static ObstacleInfo? FindNearestThreatOnHamsterLane(BotSceneSnapshot snapshot)
        {
            ObstacleInfo? nearest = null;
            bool hamsterOnBottom = snapshot.HamsterOnBottom;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0) continue;

                // Угроза на той же линии: hamsterOnBottom == !obs.IsTopLane
                bool sameLane = (hamsterOnBottom == !obs.IsTopLane);
                if (!sameLane) continue;

                if (nearest == null || obs.DistanceToHamster < nearest.Value.DistanceToHamster)
                    nearest = obs;
            }
            return nearest;
        }

        /// <summary>
        /// Проверяет, нет ли угроз на целевой линии среди всех видимых объектов.
        /// Спецификация: "другая линия безопасна (нет Threat в зоне видимости)".
        /// </summary>
        private static bool IsOtherLaneSafe(BotSceneSnapshot snapshot, bool checkOnBottom, int excludeId)
        {
            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == excludeId) continue;
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != checkOnBottom) continue;

                // Любая видимая угроза на целевой линии делает SwitchLane небезопасным
                return false;
            }
            return true;
        }

        /// <summary>
        /// Проверяет, свободна ли зона приземления прыжка.
        /// </summary>
        private static bool IsLandingClear(BotSceneSnapshot snapshot, bool hamsterOnBottom, int excludeId)
        {
            float checkFrom = snapshot.HamsterRightX + JumpLandingOffset - JumpLandingMargin;
            float checkTo   = snapshot.HamsterRightX + JumpLandingOffset + JumpLandingMargin;

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
    }
}
