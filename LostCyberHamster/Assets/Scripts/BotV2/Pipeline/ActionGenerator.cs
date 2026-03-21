using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using UnityEngine;

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
        private const float JumpOnBounceTravel = 3.5f;
        private const float JumpOnRightToleranceRatio = 0.2f;
        private const float JumpLateFallbackDistance = 0.1f;

        /// <summary>Travel during SwitchLane return-to-control phase (0.47s × GameSpeed).</summary>
        private const float SwitchLaneReturnControlTravel = 0.47f * Assets.Scripts.Consts.GameSpeedBase;
        /// <summary>Минимальная дистанция fire SwitchLane к Target: после перестроения должно остаться место для Jump.</summary>
        private const float SwitchLaneTargetMinFireDist = SwitchLaneReturnControlTravel + JumpFireDist;

        public List<ChainStep> Generate(BotSceneSnapshot snapshot)
        {
            var result = new List<ChainStep>();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var target = snapshot.VisibleObjects[i];
                if (!IsRelevantForDecision(snapshot, target))
                    continue;

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
            bool isNearest = IsNearestSameLaneThreat(snapshot, threat);

            if (isNearest &&
                TryBuildSwitchLaneStep(snapshot, threat, out ChainStep switchLaneStep, ThreatProfitScore))
            {
                result.Add(switchLaneStep);
            }

            // Jump/SuperJump для same-lane threat генерируем только для ближайшей:
            // дальние угрозы обрабатываются на следующих шагах цепочки после проекции.
            if (!isNearest)
                return;

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
                    if (snapshot.Energy >= JumpEnergyCost && IsRoofSurfaceClear(snapshot, threat) && IsRunFromRoofSafe(snapshot, threat))
                    {
                        result.Add(new ChainStep(
                            BotAction.Jump,
                            threat,
                            JumpFireDist,
                            JumpEnergyCost,
                            $"Jump on roof {threat.Type}",
                            ThreatProfitScore,
                            DecisionRank.ThreatSafety,
                            StepSemantic.JumpOnRoof));
                    }
                    break;

                case ObstacleTypeEnum.bigAlive:
                    // bigAlive ground threats are handled exclusively via SwitchLane
                    // (generated above). SuperJump on bigAlive causes SuperJumpDamage
                    // state from which the pipeline cannot recover.
                    break;
            }
        }

        private static void AddTargetVariants(List<ChainStep> result, BotSceneSnapshot snapshot, ObstacleInfo target)
        {
            bool sameLane = IsOnSameLane(snapshot, target);

            if (sameLane)
            {
                // Если между хомяком и target есть same-lane threat —
                // нельзя прыгать, сначала нужно разобраться с ближним threat (через цепочку).
                if (HasCloserSameLaneThreat(snapshot, target))
                    return;

                if (snapshot.Energy >= JumpEnergyCost)
                {
                    if (TryPredictTargetJumpSemantic(snapshot, target, out StepSemantic semantic))
                    {
                        string reason = semantic == StepSemantic.JumpOnBounce
                            ? $"Jump on target {target.Type}"
                            : $"Jump over target {target.Type}";

                    result.Add(new ChainStep(
                        BotAction.Jump,
                        target,
                        JumpFireDist,
                        JumpEnergyCost,
                            reason,
                        TargetProfitScore,
                        DecisionRank.Target,
                            semantic));
                    }
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
                        DecisionRank.Target,
                        StepSemantic.SuperJumpOver));
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
                ResolveDecisionRank(target),
                StepSemantic.SwitchLane);

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
                if (!RequiresTargetLaneClearance(obstacle))
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

            float minFireDist = target.Category == ObjectCategory.Target
                ? SwitchLaneTargetMinFireDist
                : SwitchLaneLatestSafeDist;

            float maxFireDistAllowedByClearance = target.DistanceToHamster - requiredDelayDistance;
            if (maxFireDistAllowedByClearance < minFireDist)
                return false;

            if (target.DistanceToHamster < minFireDist)
                return false;

            float preferredFireDist = target.Category == ObjectCategory.Target
                ? SwitchLaneTargetMinFireDist
                : SwitchLaneFireDist;

            executeAtDistance = preferredFireDist;
            if (executeAtDistance > maxFireDistAllowedByClearance)
                executeAtDistance = maxFireDistAllowedByClearance;
            if (executeAtDistance < minFireDist)
                executeAtDistance = minFireDist;

            return true;
        }

        private static bool RequiresTargetLaneClearance(ObstacleInfo obstacle)
        {
            switch (obstacle.Type)
            {
                // bigNotAlive/mediumNotAlive are wide obstacles that genuinely block
                // the target lane for extended periods.
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return true;

                // bigAlive has a narrow collider (~1u) comparable to small obstacles.
                // The detailed IsSwitchLaneSafeAtDistance check handles collision
                // during the lane switch animation; blocking here is overly
                // conservative and forces unnecessary Jumps that deplete energy.
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                case ObstacleTypeEnum.smallAlive:
                    return false;

                default:
                    return true;
            }
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

        /// <summary>
        /// Проверяет, есть ли same-lane threat ближе, чем указанный объект.
        /// Используется для блокировки Jump/SuperJump к далёким целям, когда
        /// на пути есть необработанная угроза.
        /// </summary>
        private static bool HasCloserSameLaneThreat(BotSceneSnapshot snapshot, ObstacleInfo target)
        {
            if (target.DistanceToHamster <= 0f)
                return false;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId == target.StableId)
                    continue;
                if (obstacle.Category != ObjectCategory.Threat)
                    continue;
                if (obstacle.DistanceToHamster < 0f)
                    continue;
                if (!IsOnSameLane(snapshot, obstacle))
                    continue;

                if (obstacle.DistanceToHamster < target.DistanceToHamster)
                    return true;
            }

            return false;
        }

        private static bool IsNearestSameLaneThreat(BotSceneSnapshot snapshot, ObstacleInfo threat)
        {
            if (threat.Category != ObjectCategory.Threat)
                return false;
            if (!IsOnSameLane(snapshot, threat))
                return false;

            float nearestDistance = float.MaxValue;
            int nearestId = int.MaxValue;

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
                {
                    nearestDistance = obstacle.DistanceToHamster;
                    nearestId = obstacle.StableId;
                    continue;
                }

                if (Mathf.Approximately(obstacle.DistanceToHamster, nearestDistance) &&
                    obstacle.StableId < nearestId)
                {
                    nearestId = obstacle.StableId;
                }
            }

            return threat.StableId == nearestId;
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
                DecisionRank.ThreatSafety,
                StepSemantic.JumpOver));
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
                DecisionRank.ThreatSafety,
                StepSemantic.SuperJumpOver));
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
        /// Проверяет, что на поверхности крыши bigNotAlive нет smallNotAliveRoadAndRoof.
        /// </summary>
        private static bool IsRoofSurfaceClear(BotSceneSnapshot snapshot, ObstacleInfo roofObject)
        {
            bool roofOnBottom = !roofObject.IsTopLane;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == roofObject.StableId) continue;
                if (obs.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != roofOnBottom) continue;

                if (obs.LeftX < roofObject.RightX && obs.RightX > roofObject.LeftX)
                    return false;
            }

            return true;
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

        private static bool TryPredictTargetJumpSemantic(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            out StepSemantic semantic)
        {
            semantic = StepSemantic.JumpOver;

            if (target.Type != ObstacleTypeEnum.smallAlive)
                return true;

            float latestFireDistance = Mathf.Min(target.DistanceToHamster, JumpFireDist);
            if (latestFireDistance < JumpLateFallbackDistance)
                return false;

            float passiveWorldShift = target.DistanceToHamster - latestFireDistance;
            if (passiveWorldShift < 0f)
                passiveWorldShift = 0f;

            if (WillJumpLandOnSmallAlive(snapshot, target, passiveWorldShift))
            {
                semantic = StepSemantic.JumpOnBounce;
                return true;
            }

            if (WillJumpOverSmallAlive(snapshot, target, passiveWorldShift))
            {
                semantic = StepSemantic.JumpOver;
                return true;
            }

            return false;
        }

        private static bool WillJumpLandOnSmallAlive(BotSceneSnapshot snapshot, ObstacleInfo target, float passiveWorldShift)
        {
            float rightTolerance = snapshot.HamsterWidth * JumpOnRightToleranceRatio;

            float obstacleLeftAtLanding = target.LeftX - passiveWorldShift - JumpLandingOffset;
            float obstacleRightAtLanding = target.RightX - passiveWorldShift - JumpLandingOffset + rightTolerance;
            float hamsterCenterX = snapshot.HamsterRightX - (snapshot.HamsterWidth * 0.5f);

            return hamsterCenterX >= obstacleLeftAtLanding && hamsterCenterX <= obstacleRightAtLanding;
        }

        private static bool WillJumpOverSmallAlive(BotSceneSnapshot snapshot, ObstacleInfo target, float passiveWorldShift)
        {
            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            float hamsterRightX = snapshot.HamsterRightX;

            float obstacleLeftAtFire = target.LeftX - passiveWorldShift;
            float obstacleRightAtFire = target.RightX - passiveWorldShift;
            float obstacleLeftAtLanding = obstacleLeftAtFire - JumpLandingOffset;
            float obstacleRightAtLanding = obstacleRightAtFire - JumpLandingOffset;

            bool clearStart = hamsterRightX < obstacleLeftAtFire;
            bool clearEnd = hamsterLeftX > obstacleRightAtLanding;
            bool noLandingOverlap = !RangesOverlap(
                hamsterLeftX,
                hamsterRightX,
                obstacleLeftAtLanding,
                obstacleRightAtLanding);

            return clearStart && clearEnd && noLandingOverlap;
        }

        private static bool RangesOverlap(float minA, float maxA, float minB, float maxB)
        {
            float start = Mathf.Max(minA, minB);
            float end = Mathf.Min(maxA, maxB);
            return start <= end;
        }
    }
}
