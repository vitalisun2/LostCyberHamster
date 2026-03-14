using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Генерирует список безопасных действий для ближайшей угрозы на линии хомяка.
    /// Этап 2: один объект, все типы Threat, выбор по энергоэффективности.
    /// Работает только со snapshot-данными.
    /// </summary>
    public class ActionGenerator
    {
        private const float SwitchLaneFireDist = 4.0f;
        internal const float SwitchLaneLatestSafeDist = 1.5f;
        private const float JumpFireDist       = 1.5f;
        private const float SuperJumpFireDist  = 1.5f;
        private const int JumpEnergyCost       = 10;
        private const int SuperJumpEnergyCost  = 20;

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

            if (ShouldGenerateSwitchLane(threat))
            {
                if (TryBuildSwitchLaneStep(snapshot, threat, out ChainStep switchLaneStep))
                {
                    result.Add(switchLaneStep);
                }
            }

            AddActionVariants(result, snapshot, threat);

            return result;
        }

        private static void AddActionVariants(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo threat)
        {
            switch (threat.Type)
            {
                case Assets.Scripts.Common.Models.ObstacleTypeEnum.smallNotAliveRoad:
                    AddJumpVariant(result, snapshot, threat, "Jump smallNotAliveRoad");
                    AddSuperJumpVariant(result, snapshot, threat, "SuperJump smallNotAliveRoad");
                    break;

                case Assets.Scripts.Common.Models.ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    AddJumpVariant(result, snapshot, threat, "Jump smallNotAliveRoadAndRoof");
                    AddSuperJumpVariant(result, snapshot, threat, "SuperJump smallNotAliveRoadAndRoof");
                    break;

                case Assets.Scripts.Common.Models.ObstacleTypeEnum.bigNotAlive:
                case Assets.Scripts.Common.Models.ObstacleTypeEnum.mediumNotAlive:
                    if (snapshot.Energy >= JumpEnergyCost)
                    {
                        result.Add(new ChainStep(
                            BotAction.Jump,
                            threat,
                            JumpFireDist,
                            JumpEnergyCost,
                            $"Jump on roof {threat.Type}"));
                    }
                    break;

                case Assets.Scripts.Common.Models.ObstacleTypeEnum.bigAlive:
                    AddSuperJumpVariant(result, snapshot, threat, "SuperJump bigAlive");
                    break;
            }
        }

        private static bool ShouldGenerateSwitchLane(ObstacleInfo threat) => true;

        private static bool TryBuildSwitchLaneStep(BotSceneSnapshot snapshot, ObstacleInfo threat, out ChainStep step)
        {
            step = null;

            if (!TryComputeSwitchLaneExecuteDistance(snapshot, threat, out float executeAtDistance))
                return false;

            if (!IsSwitchLaneSafeAtDistance(snapshot, threat, executeAtDistance))
                return false;

            step = new ChainStep(
                BotAction.SwitchLane,
                threat,
                executeAtDistance,
                energyCost: 0,
                "SwitchLane away from threat (timed window)");

            return true;
        }

        /// <summary>
        /// Вычисляет момент исполнения SwitchLane: ждём освобождения target-линиии,
        /// но не позже дедлайна, когда lane switch становится рискованным.
        /// </summary>
        private static bool TryComputeSwitchLaneExecuteDistance(
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            out float executeAtDistance)
        {
            executeAtDistance = 0f;

            float speed = Assets.Scripts.Consts.GameSpeedBase;
            if (speed <= 0f)
                return false;

            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            bool targetIsBottom = !snapshot.HamsterOnBottom;

            float requiredDelayDistance = 0f;
            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat)
                    continue;
                if (obstacle.DistanceToHamster < 0f)
                    continue;

                bool obstacleOnBottom = !obstacle.IsTopLane;
                if (obstacleOnBottom != targetIsBottom)
                    continue;

                // Нас интересуют только препятствия, которые ещё не прошли левую грань хомяка.
                if (obstacle.RightX <= hamsterLeftX)
                    continue;

                float delayDistance = obstacle.RightX - hamsterLeftX;
                if (delayDistance > requiredDelayDistance)
                    requiredDelayDistance = delayDistance;
            }

            float delayedFireDist = SwitchLaneFireDist - requiredDelayDistance;
            executeAtDistance = Clamp(delayedFireDist, SwitchLaneLatestSafeDist, SwitchLaneFireDist);

            // Если угроза уже слишком близко, SwitchLane как стратегия уже не надёжен.
            if (threat.DistanceToHamster < SwitchLaneLatestSafeDist)
                return false;

            // Не откладываем дальше текущей дистанции до угрозы, чтобы шаг мог исполниться сразу.
            if (executeAtDistance > threat.DistanceToHamster)
                executeAtDistance = threat.DistanceToHamster;

            return true;
        }

        private static bool IsSwitchLaneSafeAtDistance(
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            float executeAtDistance)
        {
            float speed = Assets.Scripts.Consts.GameSpeedBase;
            if (speed <= 0f)
                return false;

            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            float hamsterRightX = snapshot.HamsterRightX;
            bool targetIsBottom = !snapshot.HamsterOnBottom;

            float distanceDelta = threat.DistanceToHamster - executeAtDistance;
            if (distanceDelta < 0f)
                distanceDelta = 0f;
            float timeToExecute = distanceDelta / speed;
            float worldShift = timeToExecute * speed;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId == threat.StableId)
                    continue;
                if (obstacle.Category != ObjectCategory.Threat)
                    continue;

                bool obstacleOnBottom = !obstacle.IsTopLane;
                if (obstacleOnBottom != targetIsBottom)
                    continue;

                float projectedLeftX = obstacle.LeftX - worldShift;
                float projectedRightX = obstacle.RightX - worldShift;
                if (projectedRightX < hamsterLeftX)
                    continue;

                if (SwitchLaneSafety.WouldHitDuringTargetLanePhase(
                    hamsterLeftX,
                    hamsterRightX,
                    projectedLeftX,
                    projectedRightX))
                    return false;
            }

            return true;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void AddJumpVariant(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo threat, string reason)
        {
            if (snapshot.Energy < JumpEnergyCost)
                return;

            if (!IsLandingClear(snapshot, snapshot.HamsterOnBottom, threat.StableId))
                return;

            result.Add(new ChainStep(
                BotAction.Jump,
                threat,
                JumpFireDist,
                JumpEnergyCost,
                reason));
        }

        private static void AddSuperJumpVariant(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo threat, string reason)
        {
            if (snapshot.Energy < SuperJumpEnergyCost)
                return;

            result.Add(new ChainStep(
                BotAction.SuperJump,
                threat,
                SuperJumpFireDist,
                SuperJumpEnergyCost,
                reason));
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
