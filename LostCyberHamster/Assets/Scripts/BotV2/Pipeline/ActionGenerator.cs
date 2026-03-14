using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Генерирует список безопасных действий для ближайшего релевантного объекта.
    /// Этап 3: один объект, все категории (Threat/Target/Collectible).
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
        private const int ThreatProfitScore = 0;
        private const int TargetProfitScore = 100;
        private const int CollectibleBaseProfitScore = 40;

        /// <summary>Примерное расстояние, на которое хомяк улетает при Jump.</summary>
        private const float JumpLandingOffset = 3.8f;
        private const float JumpLandingMargin = 1.2f;

        public List<ChainStep> Generate(BotSceneSnapshot snapshot)
        {
            var result = new List<ChainStep>();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var target = snapshot.VisibleObjects[i];
                if (!IsRelevantForDecision(snapshot, target))
                    continue;

                AddVariantsForObject(result, snapshot, target);
                if (result.Count > 0)
                    break;
            }

            return result;
        }

        private static void AddVariantsForObject(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo target)
        {
            switch (target.Category)
            {
                case ObjectCategory.Threat:
                    AddThreatVariants(result, snapshot, target);
                    break;

                case ObjectCategory.Target:
                    AddTargetVariants(result, snapshot, target);
                    break;

                case ObjectCategory.Collectible:
                    AddCollectibleVariants(result, snapshot, target);
                    break;
            }
        }

        private static void AddThreatVariants(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo threat)
        {
            if (ShouldGenerateSwitchLane(threat))
            {
                if (TryBuildSwitchLaneStep(snapshot, threat, out ChainStep switchLaneStep, ThreatProfitScore))
                {
                    result.Add(switchLaneStep);
                }
            }

            switch (threat.Type)
            {
                case ObstacleTypeEnum.smallNotAliveRoad:
                    AddJumpVariant(result, snapshot, threat, "Jump smallNotAliveRoad", ThreatProfitScore);
                    AddSuperJumpVariant(result, snapshot, threat, "SuperJump smallNotAliveRoad", ThreatProfitScore);
                    break;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    AddJumpVariant(result, snapshot, threat, "Jump smallNotAliveRoadAndRoof", ThreatProfitScore);
                    AddSuperJumpVariant(result, snapshot, threat, "SuperJump smallNotAliveRoadAndRoof", ThreatProfitScore);
                    break;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    if (snapshot.Energy >= JumpEnergyCost)
                    {
                        result.Add(new ChainStep(
                            BotAction.Jump,
                            threat,
                            JumpFireDist,
                            JumpEnergyCost,
                            $"Jump on roof {threat.Type}",
                            ThreatProfitScore));
                    }
                    break;

                case ObstacleTypeEnum.bigAlive:
                    AddSuperJumpVariant(result, snapshot, threat, "SuperJump bigAlive", ThreatProfitScore);
                    break;
            }
        }

        private static void AddTargetVariants(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo target)
        {
            bool sameLane = IsOnSameLane(snapshot, target);

            if (sameLane)
            {
                if (snapshot.Energy >= JumpEnergyCost)
                {
                    result.Add(new ChainStep(
                        BotAction.Jump,
                        target,
                        JumpFireDist,
                        JumpEnergyCost,
                        $"Jump on target {target.Type}",
                        TargetProfitScore));
                }

                if (target.Type == ObstacleTypeEnum.bigAlive && snapshot.Energy >= SuperJumpEnergyCost)
                {
                    result.Add(new ChainStep(
                        BotAction.SuperJump,
                        target,
                        SuperJumpFireDist,
                        SuperJumpEnergyCost,
                        "SuperJump target bigAlive",
                        TargetProfitScore - 5));
                }
            }
            else
            {
                if (TryBuildSwitchLaneStep(snapshot, target, out ChainStep switchLaneStep, TargetProfitScore - 20))
                {
                    switchLaneStep.Reason = "SwitchLane to target";
                    result.Add(switchLaneStep);
                }
            }
        }

        private static void AddCollectibleVariants(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo collectible)
        {
            if (IsOnSameLane(snapshot, collectible))
                return;

            int collectibleProfit = CollectibleBaseProfitScore + GetCollectiblePriority(collectible.Type);
            if (TryBuildSwitchLaneStep(snapshot, collectible, out ChainStep switchLaneStep, collectibleProfit))
            {
                switchLaneStep.Reason = $"SwitchLane collect {collectible.Type}";
                result.Add(switchLaneStep);
            }
        }

        private static bool ShouldGenerateSwitchLane(ObstacleInfo threat) => true;

        private static bool TryBuildSwitchLaneStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            out ChainStep step,
            int profitScore)
        {
            step = null;

            if (!TryComputeSwitchLaneExecuteDistance(snapshot, target, out float executeAtDistance))
                return false;

            if (!IsSwitchLaneSafeAtDistance(snapshot, target, executeAtDistance))
                return false;

            step = new ChainStep(
                BotAction.SwitchLane,
                target,
                executeAtDistance,
                energyCost: 0,
                "SwitchLane away from threat (timed window)",
                profitScore);

            return true;
        }

        /// <summary>
        /// Вычисляет момент исполнения SwitchLane: ждём освобождения target-линиии,
        /// но не позже дедлайна, когда lane switch становится рискованным.
        /// </summary>
        private static bool TryComputeSwitchLaneExecuteDistance(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
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

            if (target.DistanceToHamster < SwitchLaneLatestSafeDist)
                return false;

            if (executeAtDistance > target.DistanceToHamster)
                executeAtDistance = target.DistanceToHamster;

            return true;
        }

        private static bool IsSwitchLaneSafeAtDistance(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            float executeAtDistance)
        {
            float speed = Assets.Scripts.Consts.GameSpeedBase;
            if (speed <= 0f)
                return false;

            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            float hamsterRightX = snapshot.HamsterRightX;
            bool targetIsBottom = !snapshot.HamsterOnBottom;

            float distanceDelta = target.DistanceToHamster - executeAtDistance;
            if (distanceDelta < 0f)
                distanceDelta = 0f;
            float timeToExecute = distanceDelta / speed;
            float worldShift = timeToExecute * speed;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId == target.StableId)
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

        private static void AddJumpVariant(
            List<ChainStep> result,
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            string reason,
            int profitScore)
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
                reason,
                profitScore));
        }

        private static void AddSuperJumpVariant(
            List<ChainStep> result,
            BotSceneSnapshot snapshot,
            ObstacleInfo threat,
            string reason,
            int profitScore)
        {
            if (snapshot.Energy < SuperJumpEnergyCost)
                return;

            result.Add(new ChainStep(
                BotAction.SuperJump,
                threat,
                SuperJumpFireDist,
                SuperJumpEnergyCost,
                reason,
                profitScore));
        }

        private static bool IsRelevantForDecision(BotSceneSnapshot snapshot, ObstacleInfo obs)
        {
            if (obs.Category == ObjectCategory.Neutral)
                return false;

            if (obs.DistanceToHamster < 0f)
                return false;

            if (obs.Category == ObjectCategory.Threat && !IsOnSameLane(snapshot, obs))
                return false;

            return true;
        }

        private static bool IsOnSameLane(BotSceneSnapshot snapshot, ObstacleInfo obstacle)
        {
            return snapshot.HamsterOnBottom == !obstacle.IsTopLane;
        }

        private static int GetCollectiblePriority(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.collectableLife:
                    return 8;
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                    return 6;
                case ObstacleTypeEnum.collectableCrystal:
                    return 4;
                case ObstacleTypeEnum.collectableCoin:
                    return 2;
                default:
                    return 1;
            }
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
