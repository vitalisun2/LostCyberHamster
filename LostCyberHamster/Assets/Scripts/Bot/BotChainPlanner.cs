using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Chain Planner — основной алгоритм бота.
    /// Сканирует объекты, классифицирует, строит цепочку действий.
    /// </summary>
    public class BotChainPlanner
    {
        // ──────────────── Константы ────────────────

        private const float ScanBehindMargin = 1.0f;
        private const float SafeMargin = 1.5f;
        private const float LaneSwitchDuration = 0.3f;
        private const float LaneSwitchTravel = LaneSwitchDuration * Consts.GameSpeedBase;
        private const int JumpEnergyCost = 10;
        private const int SuperJumpEnergyCost = 20;
        private const int BuyEnergyPrice = 50;
        private const int BuyEnergyAmount = 100;

        // Приблизительные дальности приземления прыжков (в юнитах сдвига сцены)
        private const float JumpLandingTravel = 3.8f;   // ~1с * GameSpeedBase
        private const float SuperJumpLandingTravel = 4.6f; // ~1.2с * GameSpeedBase
        private const float LandingCheckTolerance = 0.8f;

        // Минимальное расстояние, с которого ульту имеет смысл использовать
        private const float UltaMinDistance = 1.0f;

        // ──────────────── Переиспользуемые буферы ────────────────

        private readonly List<ObstacleInfo> _obstacles = new(32);
        private readonly List<ChainStep> _chain = new(8);

        // ──────────────── Публичные результаты ────────────────

        /// <summary>Текущий список отсканированных объектов (readonly view).</summary>
        public IReadOnlyList<ObstacleInfo> Obstacles => _obstacles;

        /// <summary>Текущая построенная цепочка.</summary>
        public IReadOnlyList<ChainStep> Chain => _chain;

        // ══════════════════════════════════════════════
        //  Сканирование
        // ══════════════════════════════════════════════

        /// <summary>
        /// Сканирует спавнер, заполняет внутренний буфер ObstacleInfo[].
        /// </summary>
        public void ScanObstacles(Hamster hamster, float scanRange)
        {
            _obstacles.Clear();

            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return;

            var spawned = spawner.SpawnedObstacles;
            float hamsterRightX = hamster.RightX;
            float hamsterLeftX = hamster.LeftX;
            float maxX = hamsterRightX + scanRange;
            float minX = hamsterLeftX - ScanBehindMargin;

            bool hamsterOnBottom = hamster.IsOnBottomLine.Value;
            bool hamsterOnRoof = IsRoofState(hamster.HamsterState.Value);

            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                var obs = inst.ObstacleScript;
                var pos = obs.transform.position;
                float halfW = obs.ColliderWidth * 0.5f;
                float leftX = pos.x - halfW;
                float rightX = pos.x + halfW;

                if (rightX < minX || leftX > maxX) continue;

                var typeEnum = obs.ObstacleType.ObstacleTypeEnum;
                bool isTopLane = obs.ObstacleType.IsTop;
                bool isOnRoof = IsOnRoof(pos.y, isTopLane);

                float distance = leftX - hamsterRightX;
                float timeToReach = distance > 0
                    ? distance / Consts.GameSpeedBase
                    : 0f;

                var category = Classify(typeEnum, isTopLane, isOnRoof,
                    hamsterOnBottom, hamsterOnRoof, distance);

                _obstacles.Add(new ObstacleInfo(
                    typeEnum, leftX, rightX, pos.x,
                    isTopLane, isOnRoof,
                    distance, timeToReach, category));
            }

            _obstacles.Sort((a, b) => a.LeftX.CompareTo(b.LeftX));
        }

        // ══════════════════════════════════════════════
        //  Основная точка входа: BuildChain
        // ══════════════════════════════════════════════

        /// <summary>
        /// Строит цепочку действий. Порядок приоритетов:
        /// 1. Ульта (если готова и выгодно)
        /// 2. Попытка напрыгнуть на Target
        /// 3. Уклонение от Threat
        /// 4. Сбор ценных Bonus (если безопасно)
        /// Возвращает true, если цепочка не пуста.
        /// </summary>
        public bool BuildChain(Hamster hamster)
        {
            _chain.Clear();

            if (_obstacles.Count == 0) return false;

            bool hamsterOnBottom = hamster.IsOnBottomLine.Value;
            bool hamsterOnRoof = IsRoofState(hamster.HamsterState.Value);
            int energy = hamster.Energy.Value;
            int lives = hamster.Lives.Value;
            int ulta = hamster.UltaChargeAmount.Value;

            // ── Этап 6: Ульта ──
            if (ulta >= 100 && TryBuildUltaChain(hamsterOnBottom, hamsterOnRoof, lives))
                return true;

            // ── Этап 4: Напрыгивание на Target ──
            if (TryBuildTargetChain(hamsterOnBottom, hamsterOnRoof, energy, hamster))
                return true;

            // ── Этап 3: Уклонение от Threat ──
            if (TryBuildEvasionChain(hamsterOnBottom, hamsterOnRoof, energy, hamster))
                return true;

            // ── Этап 7: Сбор Bonus (если текущая линия чистая) ──
            if (TryBuildBonusChain(hamsterOnBottom, hamsterOnRoof))
                return true;

            // ── Этап 10: Логирование непроходимых ситуаций ──
            LogNoSafePath(hamsterOnBottom, hamsterOnRoof, energy, lives, ulta);

            return false;
        }

        // ══════════════════════════════════════════════
        //  Этап 6: Ульта
        // ══════════════════════════════════════════════

        private bool TryBuildUltaChain(bool hamsterOnBottom, bool hamsterOnRoof, int lives)
        {
            // Считаем угрозы на текущей линии в ближайшей зоне
            int nearThreats = 0;
            bool unavoidableThreat = false;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.DistanceToHamster < 0 || obs.DistanceToHamster > 6f) continue;
                if (obs.Category != ObjectCategory.Threat) continue;
                if (!IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                    continue;

                nearThreats++;

                // Неизбежная угроза: слишком близко и другая линия тоже заблокирована
                if (obs.DistanceToHamster < SafeMargin + 0.5f &&
                    !IsOtherLaneSafe(obs, hamsterOnBottom))
                    unavoidableThreat = true;
            }

            // Кластер из 2+ угроз или при 1 жизни неизбежная угроза
            if (nearThreats >= 2 || (lives <= 1 && unavoidableThreat))
            {
                _chain.Add(new ChainStep(BotAction.UseUlta, -1, 0, 0,
                    $"Ulta: {nearThreats} threats, lives={lives}"));
                return true;
            }

            return false;
        }

        // ══════════════════════════════════════════════
        //  Этап 4: Напрыгивание на Target
        // ══════════════════════════════════════════════

        private bool TryBuildTargetChain(bool hamsterOnBottom, bool hamsterOnRoof,
            int energy, Hamster hamster)
        {
            // Найти ближайшую Target на текущей линии
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.DistanceToHamster < 0) continue;
                if (obs.Category != ObjectCategory.Target) continue;
                if (!IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                    continue;

                int jumpCost = JumpEnergyCost;
                BotAction jumpAction = hamsterOnRoof ? BotAction.RoofJump : BotAction.Jump;

                // Проверяем угрозы между хомяком и Target
                int totalEnergyCost = jumpCost;
                bool pathBlocked = false;

                for (int j = 0; j < _obstacles.Count; j++)
                {
                    if (j == i) continue;
                    var between = _obstacles[j];
                    if (between.DistanceToHamster < 0) continue;
                    if (between.LeftX >= obs.LeftX) break; // Дальше Target — не на пути

                    if (!IsSameLane(between.IsTopLane, hamsterOnBottom, between.IsOnRoof, hamsterOnRoof))
                        continue;

                    if (between.Category == ObjectCategory.Threat)
                    {
                        // Есть угроза перед Target — путь заблокирован для простого прыжка
                        pathBlocked = true;
                        break;
                    }
                }

                if (pathBlocked) continue; // Попробовать следующую Target

                // Проверяем последствия: безопасно ли приземление после напрыгивания?
                if (!IsLandingSafe(obs, hamsterOnBottom, hamsterOnRoof, JumpLandingTravel))
                {
                    continue; // Небезопасно — пропускаем эту Target
                }

                // Проверяем энергию (+ аварийная покупка — Этап 8)
                if (energy < totalEnergyCost)
                {
                    if (TryAddBuyEnergyStep(hamster))
                    {
                        _chain.Add(new ChainStep(jumpAction, i, SafeMargin,
                            jumpCost, $"Jump on {obs.Type} (after buy energy)"));
                        return true;
                    }
                    continue; // Не хватает энергии и не можем купить
                }

                _chain.Add(new ChainStep(jumpAction, i, SafeMargin,
                    jumpCost, $"Jump on target {obs.Type}"));
                return true;
            }

            return false;
        }

        // ══════════════════════════════════════════════
        //  Этап 3: Уклонение
        // ══════════════════════════════════════════════

        private bool TryBuildEvasionChain(bool hamsterOnBottom, bool hamsterOnRoof,
            int energy, Hamster hamster)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.DistanceToHamster < -0.5f) continue;
                if (obs.Category != ObjectCategory.Threat) continue;
                if (!IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                    continue;

                var step = SelectEvasionTool(obs, i, hamsterOnBottom, hamsterOnRoof, energy);

                // Этап 8: если нет инструмента из-за энергии — попробовать купить
                if (step.Action == BotAction.None && energy < JumpEnergyCost)
                {
                    if (TryAddBuyEnergyStep(hamster))
                    {
                        // После покупки пересканируем: dirty flag сработает
                        return true;
                    }
                }

                if (step.Action != BotAction.None)
                {
                    _chain.Add(step);
                    return true;
                }
            }

            return false;
        }

        // ══════════════════════════════════════════════
        //  Этап 7: Сбор Bonus
        // ══════════════════════════════════════════════

        private bool TryBuildBonusChain(bool hamsterOnBottom, bool hamsterOnRoof)
        {
            // Текущая линия безопасна? (нет Threat/Target в ближайших ~4 юнитах)
            bool currentLineBusy = false;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.DistanceToHamster < 0 || obs.DistanceToHamster > 4f) continue;
                if (obs.Category == ObjectCategory.Neutral || obs.Category == ObjectCategory.Bonus) continue;
                if (IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                {
                    currentLineBusy = true;
                    break;
                }
            }

            if (currentLineBusy) return false;

            // Ищем ценный Bonus на другой линии
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.DistanceToHamster < 0.5f || obs.DistanceToHamster > 5f) continue;
                if (obs.Category != ObjectCategory.Bonus) continue;

                // Бонус на текущей линии — хомяк соберёт сам
                if (IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                    continue;

                // Приоритет: жизнь и энергетики стоят смены, монеты — нет
                if (!IsValuableBonus(obs.Type)) continue;

                // Другая линия безопасна?
                if (IsOtherLaneSafe(obs, hamsterOnBottom))
                {
                    _chain.Add(new ChainStep(BotAction.SwitchLane, i,
                        SafeMargin + 1f, 0, $"Collect bonus {obs.Type}"));
                    return true;
                }
            }

            return false;
        }

        // ══════════════════════════════════════════════
        //  Этап 8: Аварийная покупка энергии
        // ══════════════════════════════════════════════

        /// <summary>
        /// Добавляет шаг покупки энергии, если хватает монет. Возвращает true при успехе.
        /// </summary>
        private bool TryAddBuyEnergyStep(Hamster hamster)
        {
            if (!ResourceManager.CanSpendResource(ResourceType.Coins, BuyEnergyPrice))
                return false;

            ResourceManager.SpendResource(ResourceType.Coins, BuyEnergyPrice);
            hamster.AddEnergy(BuyEnergyAmount);
            DebugManager.DiagLog("[BotChainPlanner] Emergency energy purchase: -50 coins, +100 energy");
            return true;
        }

        // ══════════════════════════════════════════════
        //  Классификация
        // ══════════════════════════════════════════════

        private static ObjectCategory Classify(
            ObstacleTypeEnum type, bool isTopLane, bool isOnRoof,
            bool hamsterOnBottom, bool hamsterOnRoof, float distance)
        {
            if (distance < -0.5f) return ObjectCategory.Neutral;

            switch (type)
            {
                case ObstacleTypeEnum.decor:
                    return ObjectCategory.Neutral;

                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                case ObstacleTypeEnum.collectableCrystal:
                case ObstacleTypeEnum.collectableLife:
                case ObstacleTypeEnum.collectableCoin:
                    return ObjectCategory.Bonus;

                case ObstacleTypeEnum.smallAlive:
                    return ObjectCategory.Target;

                case ObstacleTypeEnum.bigAlive:
                    if (hamsterOnRoof)
                        return ObjectCategory.Target;
                    return ObjectCategory.Threat;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return ObjectCategory.Threat;

                case ObstacleTypeEnum.smallNotAliveRoad:
                    return ObjectCategory.Threat;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return ObjectCategory.Threat;

                default:
                    return ObjectCategory.Neutral;
            }
        }

        // ══════════════════════════════════════════════
        //  Выбор инструмента уклонения
        // ══════════════════════════════════════════════

        private ChainStep SelectEvasionTool(
            ObstacleInfo threat, int index,
            bool hamsterOnBottom, bool hamsterOnRoof, int energy)
        {
            switch (threat.Type)
            {
                case ObstacleTypeEnum.bigAlive:
                    if (IsOtherLaneSafe(threat, hamsterOnBottom))
                        return new ChainStep(BotAction.SwitchLane, index,
                            SafeMargin, 0, "Evade bigAlive: switch lane");
                    if (energy >= SuperJumpEnergyCost)
                        return new ChainStep(
                            hamsterOnRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                            index, SafeMargin, SuperJumpEnergyCost,
                            "Evade bigAlive: super jump");
                    break;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    if (!hamsterOnRoof && energy >= JumpEnergyCost)
                    {
                        // Проверяем: есть ли SmallNotAliveRoadAndRoof на крыше?
                        bool roofClear = !HasRoofObstacle(threat);
                        if (roofClear)
                            return new ChainStep(BotAction.Jump, index,
                                SafeMargin, JumpEnergyCost,
                                $"Evade {threat.Type}: jump on roof");
                    }
                    if (IsOtherLaneSafe(threat, hamsterOnBottom))
                        return new ChainStep(BotAction.SwitchLane, index,
                            SafeMargin, 0, $"Evade {threat.Type}: switch lane");
                    // Крыша занята, другая линия тоже — прыжок на крышу всё равно (лучше чем ничего)
                    if (!hamsterOnRoof && energy >= JumpEnergyCost)
                        return new ChainStep(BotAction.Jump, index,
                            SafeMargin, JumpEnergyCost,
                            $"Evade {threat.Type}: jump on roof (roof has obstacle)");
                    break;

                case ObstacleTypeEnum.smallNotAliveRoad:
                    if (IsOtherLaneSafe(threat, hamsterOnBottom))
                        return new ChainStep(BotAction.SwitchLane, index,
                            SafeMargin, 0, "Evade smallNotAliveRoad: switch lane");
                    if (energy >= JumpEnergyCost)
                    {
                        var action = hamsterOnRoof ? BotAction.RoofJump : BotAction.Jump;
                        if (IsLandingSafe(threat, hamsterOnBottom, hamsterOnRoof, JumpLandingTravel))
                            return new ChainStep(action, index,
                                SafeMargin, JumpEnergyCost,
                                "Evade smallNotAliveRoad: jump over");
                    }
                    break;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    if (hamsterOnRoof)
                    {
                        if (energy >= JumpEnergyCost)
                        {
                            if (IsLandingSafe(threat, hamsterOnBottom, hamsterOnRoof, JumpLandingTravel))
                                return new ChainStep(BotAction.RoofJump, index,
                                    SafeMargin, JumpEnergyCost,
                                    "Evade smallNotAliveRoadAndRoof on roof: roof jump");
                        }
                    }
                    else
                    {
                        if (IsOtherLaneSafe(threat, hamsterOnBottom))
                            return new ChainStep(BotAction.SwitchLane, index,
                                SafeMargin, 0,
                                "Evade smallNotAliveRoadAndRoof: switch lane");
                        if (energy >= JumpEnergyCost)
                        {
                            var action = BotAction.Jump;
                            if (IsLandingSafe(threat, hamsterOnBottom, hamsterOnRoof, JumpLandingTravel))
                                return new ChainStep(action, index,
                                    SafeMargin, JumpEnergyCost,
                                    "Evade smallNotAliveRoadAndRoof: jump over");
                        }
                    }
                    break;
            }

            return new ChainStep(BotAction.None, -1, 0, 0, "No tool available");
        }

        // ══════════════════════════════════════════════
        //  Проверки последствий
        // ══════════════════════════════════════════════

        /// <summary>
        /// Безопасно ли приземление после прыжка через/на объект?
        /// Проверяет: нет ли Threat в зоне приземления на той же линии.
        /// </summary>
        private bool IsLandingSafe(ObstacleInfo source,
            bool hamsterOnBottom, bool hamsterOnRoof, float landingTravel)
        {
            float landingX = source.RightX + landingTravel;
            float checkFrom = landingX - LandingCheckTolerance;
            float checkTo = landingX + LandingCheckTolerance;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.Category != ObjectCategory.Threat) continue;
                if (!IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                    continue;

                if (obs.RightX > checkFrom && obs.LeftX < checkTo)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Есть ли SmallNotAliveRoadAndRoof на крыше данного BigNotAlive/MediumNotAlive?
        /// </summary>
        private bool HasRoofObstacle(ObstacleInfo baseObs)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (!obs.IsOnRoof) continue;
                if (obs.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof) continue;
                if (obs.IsTopLane != baseObs.IsTopLane) continue;

                // Пересечение по X: объект на крыше перекрывает базовый?
                if (obs.RightX > baseObs.LeftX && obs.LeftX < baseObs.RightX)
                    return true;
            }
            return false;
        }

        // ══════════════════════════════════════════════
        //  Вспомогательные
        // ══════════════════════════════════════════════

        /// <summary>Проверяет, что другая линия безопасна для смены.</summary>
        private bool IsOtherLaneSafe(ObstacleInfo threat, bool hamsterOnBottom)
        {
            float checkFrom = threat.LeftX - LaneSwitchTravel;
            float checkTo = threat.RightX + LaneSwitchTravel;
            bool otherLaneIsTop = hamsterOnBottom;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.Category == ObjectCategory.Neutral ||
                    obs.Category == ObjectCategory.Bonus)
                    continue;

                if (obs.IsTopLane != otherLaneIsTop) continue;
                if (obs.IsOnRoof) continue;

                if (obs.RightX > checkFrom && obs.LeftX < checkTo)
                    return false;
            }

            return true;
        }

        /// <summary>Объект на той же линии, что и хомяк?</summary>
        private static bool IsSameLane(bool obsIsTop, bool hamsterOnBottom,
            bool obsOnRoof, bool hamsterOnRoof)
        {
            if (hamsterOnRoof)
                return obsOnRoof && (obsIsTop != hamsterOnBottom);

            bool hamsterIsTop = !hamsterOnBottom;
            return !obsOnRoof && obsIsTop == hamsterIsTop;
        }

        /// <summary>Определяет, стоит ли объект на крыше (по Y).</summary>
        private static bool IsOnRoof(float yPos, bool isTopLane)
        {
            float roofY = isTopLane ? Consts.ObstacleRoofY0Pos : Consts.ObstacleRoofY1Pos;
            return Mathf.Abs(yPos - roofY) < Consts.ObstacleLineTolerance;
        }

        /// <summary>Хомяк сейчас на крыше?</summary>
        private static bool IsRoofState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.RoofRun
                || state == HamsterStateEnum.RoofJump
                || state == HamsterStateEnum.RoofJumpDamage
                || state == HamsterStateEnum.SuperRoofJump
                || state == HamsterStateEnum.SuperRoofJumpDamage;
        }

        // ══════════════════════════════════════════════
        //  Этап 10: QA логирование
        // ══════════════════════════════════════════════

        private void LogNoSafePath(bool hamsterOnBottom, bool hamsterOnRoof,
            int energy, int lives, int ulta)
        {
            // Логируем только если есть реальные угрозы впереди
            bool hasNearThreat = false;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                if (_obstacles[i].Category == ObjectCategory.Threat &&
                    _obstacles[i].DistanceToHamster > 0 &&
                    _obstacles[i].DistanceToHamster < 4f &&
                    IsSameLane(_obstacles[i].IsTopLane, hamsterOnBottom,
                        _obstacles[i].IsOnRoof, hamsterOnRoof))
                {
                    hasNearThreat = true;
                    break;
                }
            }

            if (!hasNearThreat) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[BotQA] NO SAFE PATH FOUND");
            sb.AppendLine($"  Hamster: lane={( hamsterOnBottom ? "bottom" : "top")} roof={hamsterOnRoof} energy={energy} lives={lives} ulta={ulta}");
            sb.AppendLine($"  Obstacles ({_obstacles.Count}):");
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var o = _obstacles[i];
                if (o.DistanceToHamster < -1f || o.DistanceToHamster > 6f) continue;
                sb.AppendLine($"    [{i}] {o.Type} cat={o.Category} dist={o.DistanceToHamster:F2} lane={( o.IsTopLane ? "top" : "bottom")} roof={o.IsOnRoof}");
            }
            DebugManager.DiagLog(sb.ToString());
        }

        /// <summary>Стоит ли менять линию ради этого бонуса?</summary>
        private static bool IsValuableBonus(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.collectableLife
                || type == ObstacleTypeEnum.collectableEnergetic
                || type == ObstacleTypeEnum.collectablePizza
                || type == ObstacleTypeEnum.collectableCrystal;
        }
    }
}
