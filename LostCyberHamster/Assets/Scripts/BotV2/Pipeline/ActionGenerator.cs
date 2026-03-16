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
        private const float DecisionClusterDistanceWindow = 3.5f;
        private const float LifeCollectibleExtraWindow = 8f;
        private const float LifeCollectibleMaxSwitchFireDist = 8f;
        private const float LifeCollectibleMinGenerateDist = 0.8f;
        private const float LifeUrgentSourceThreatDistance = 5.5f;
        private const float JumpFireDist       = 1.5f;
        private const float SuperJumpFireDist  = 1.5f;
        internal const int JumpEnergyCost      = 10;
        private const int SuperJumpEnergyCost  = 20;
        private const int ThreatProfitScore = 0;
        private const int TargetProfitScore = 100;
        private const int CollectibleBaseProfitScore = 30;

        /// <summary>Примерное расстояние, на которое хомяк улетает при Jump.</summary>
        private const float JumpLandingOffset = 3.8f;
        private const float JumpLandingMargin = 1.2f;

        public List<ChainStep> Generate(BotSceneSnapshot snapshot)
        {
            var result = new List<ChainStep>();
            float firstRelevantDistance = float.MaxValue;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var target = snapshot.VisibleObjects[i];
                if (!IsRelevantForDecision(snapshot, target))
                    continue;

                if (firstRelevantDistance == float.MaxValue)
                    firstRelevantDistance = target.DistanceToHamster;

                if (target.DistanceToHamster > firstRelevantDistance + DecisionClusterDistanceWindow)
                {
                    bool isDistantLifeCollectible =
                        target.Category == ObjectCategory.Collectible &&
                        target.Type == ObstacleTypeEnum.collectableLife &&
                        target.DistanceToHamster <= firstRelevantDistance + LifeCollectibleExtraWindow;

                    if (!isDistantLifeCollectible)
                        continue;
                }

                AddVariantsForObject(result, snapshot, target);
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
            if (TryBuildSwitchLaneStep(snapshot, threat, out ChainStep switchLaneStep, ThreatProfitScore))
            {
                result.Add(switchLaneStep);
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
                    if (snapshot.Energy >= JumpEnergyCost && IsRunFromRoofSafe(snapshot, threat))
                    {
                        result.Add(new ChainStep(
                            BotAction.Jump,
                            threat,
                            JumpFireDist,
                            JumpEnergyCost,
                            $"Jump on roof {threat.Type}",
                            ThreatProfitScore,
                            DecisionRank.ThreatSafety));
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
                        TargetProfitScore,
                        DecisionRank.Target));
                }

                if (target.Type == ObstacleTypeEnum.bigAlive && snapshot.Energy >= SuperJumpEnergyCost)
                {
                    result.Add(new ChainStep(
                        BotAction.SuperJump,
                        target,
                        SuperJumpFireDist,
                        SuperJumpEnergyCost,
                        "SuperJump target bigAlive",
                        TargetProfitScore - 5,
                        DecisionRank.Target));
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
                switchLaneStep.Rank = collectible.Type == ObstacleTypeEnum.collectableLife
                    ? DecisionRank.LifeCollectible
                    : DecisionRank.OtherCollectible;
                switchLaneStep.Reason = $"SwitchLane collect {collectible.Type}";
                result.Add(switchLaneStep);
            }
        }

        private static bool TryBuildSwitchLaneStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            out ChainStep step,
            int profitScore)
        {
            step = null;

            if (!TryComputeSwitchLaneExecuteDistance(snapshot, target, out float executeAtDistance))
                return false;

            bool safeAtSnapshot = IsSwitchLaneSafeAtDistance(snapshot, target, executeAtDistance);
            bool allowLiveRecheckForLife = target.Category == ObjectCategory.Collectible &&
                                           target.Type == ObstacleTypeEnum.collectableLife;
            if (!safeAtSnapshot && !allowLiveRecheckForLife)
                return false;

            step = new ChainStep(
                BotAction.SwitchLane,
                target,
                executeAtDistance,
                energyCost: 0,
                "SwitchLane away from threat (timed window)",
                profitScore,
                ResolveDecisionRank(target));

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

            if (target.Category == ObjectCategory.Collectible &&
                target.Type == ObstacleTypeEnum.collectableLife)
            {
                // Для life: если в текущей полосе есть близкая угроза, перестраиваемся сразу,
                // не дожидаясь приближения collectable по дистанции.
                if (TryGetNearestSameLaneThreatDistance(snapshot, out float nearestThreatDistance) &&
                    nearestThreatDistance <= LifeUrgentSourceThreatDistance)
                {
                    executeAtDistance = target.DistanceToHamster;
                    return target.DistanceToHamster >= LifeCollectibleMinGenerateDist;
                }

                // В обычной ситуации начинаем проверять переключение заметно раньше базового окна.
                executeAtDistance = Clamp(target.DistanceToHamster, LifeCollectibleMinGenerateDist, LifeCollectibleMaxSwitchFireDist);
                return target.DistanceToHamster >= LifeCollectibleMinGenerateDist;
            }

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
            bool sourceIsBottom = snapshot.HamsterOnBottom;
            bool targetIsBottom = !snapshot.HamsterOnBottom;

            float distanceDelta = target.DistanceToHamster - executeAtDistance;
            if (distanceDelta < 0f)
                distanceDelta = 0f;
            float timeToExecute = distanceDelta / speed;
            float worldShift = timeToExecute * speed;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId == target.StableId && !SwitchLaneSafety.IsHazardForSwitchLane(obstacle))
                    continue;
                if (!SwitchLaneSafety.IsHazardForSwitchLane(obstacle))
                    continue;

                bool obstacleOnBottom = !obstacle.IsTopLane;
                if (obstacleOnBottom != sourceIsBottom && obstacleOnBottom != targetIsBottom)
                    continue;

                float projectedLeftX = obstacle.LeftX - worldShift;
                float projectedRightX = obstacle.RightX - worldShift;
                if (projectedRightX < hamsterLeftX)
                    continue;

                if (obstacleOnBottom == sourceIsBottom)
                {
                    if (SwitchLaneSafety.WouldHitDuringSourceLanePhase(
                        hamsterLeftX,
                        hamsterRightX,
                        projectedLeftX,
                        projectedRightX))
                        return false;
                }

                if (obstacleOnBottom == targetIsBottom)
                {
                    if (SwitchLaneSafety.WouldHitDuringTargetLanePhase(
                        hamsterLeftX,
                        hamsterRightX,
                        projectedLeftX,
                        projectedRightX))
                        return false;
                }
            }

            return true;
        }

        private static bool TryGetNearestSameLaneThreatDistance(BotSceneSnapshot snapshot, out float nearestDistance)
        {
            nearestDistance = float.MaxValue;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat)
                    continue;
                if (obstacle.DistanceToHamster < 0f)
                    continue;
                if (!IsOnSameLane(snapshot, obstacle))
                    continue;

                if (obstacle.DistanceToHamster < nearestDistance)
                    nearestDistance = obstacle.DistanceToHamster;
            }

            return nearestDistance < float.MaxValue;
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
                profitScore,
                DecisionRank.ThreatSafety));
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
                profitScore,
                DecisionRank.ThreatSafety));
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

        private static DecisionRank ResolveDecisionRank(ObstacleInfo target)
        {
            switch (target.Category)
            {
                case ObjectCategory.Target:
                    return DecisionRank.Target;
                case ObjectCategory.Collectible:
                    return target.Type == ObstacleTypeEnum.collectableLife
                        ? DecisionRank.LifeCollectible
                        : DecisionRank.OtherCollectible;
                default:
                    return DecisionRank.ThreatSafety;
            }
        }

        private static int GetCollectiblePriority(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.collectableLife:
                    return 20;
                case ObstacleTypeEnum.collectableCrystal:
                    return 14;
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                    return 8;
                case ObstacleTypeEnum.collectableCoin:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Проверяет, свободна ли зона автоспуска с крыши (~1.9u после конца крыши).
        /// Если в этой зоне есть Threat на той же линии — JumpOnRoof небезопасен.
        /// </summary>
        private static bool IsRunFromRoofSafe(BotSceneSnapshot snapshot, ObstacleInfo roofObject)
        {
            const float RunFromRoofVulnerabilityDistance = 1.9f;

            float dangerZoneStart = roofObject.RightX;
            float dangerZoneEnd = roofObject.RightX + RunFromRoofVulnerabilityDistance;
            bool roofOnBottom = !roofObject.IsTopLane;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == roofObject.StableId) continue;
                if (obs.Category != ObjectCategory.Threat) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != roofOnBottom) continue;

                if (obs.LeftX < dangerZoneEnd && obs.RightX > dangerZoneStart)
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
