using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

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
        private const int JumpEnergyCost = 10;
        private const int SuperJumpEnergyCost = 20;

        // ──────────────── Переиспользуемые буферы ────────────────

        private readonly List<ObstacleInfo> _obstacles = new(32);
        private readonly List<ChainStep> _chain = new(8);

        // ──────────────── Публичные результаты ────────────────

        /// <summary>Текущий список отсканированных объектов (readonly view).</summary>
        public IReadOnlyList<ObstacleInfo> Obstacles => _obstacles;

        /// <summary>Текущая построенная цепочка.</summary>
        public IReadOnlyList<ChainStep> Chain => _chain;

        // ══════════════════════════════════════════════
        //  Этап 2: Сканирование
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

                // Пропускаем: за хомяком или за пределами сканирования
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

            // Сортировка по LeftX (ближайшие первыми)
            _obstacles.Sort((a, b) => a.LeftX.CompareTo(b.LeftX));
        }

        // ══════════════════════════════════════════════
        //  Этап 3: Построение цепочки уклонения
        // ══════════════════════════════════════════════

        /// <summary>
        /// Строит цепочку действий на основе отсканированных объектов.
        /// Возвращает true, если цепочка не пуста.
        /// </summary>
        public bool BuildChain(Hamster hamster)
        {
            _chain.Clear();

            if (_obstacles.Count == 0) return false;

            bool hamsterOnBottom = hamster.IsOnBottomLine.Value;
            bool hamsterOnRoof = IsRoofState(hamster.HamsterState.Value);
            int energy = hamster.Energy.Value;

            // Ищем ближайшую угрозу на текущей линии
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];

                // Пропускаем объекты позади
                if (obs.DistanceToHamster < -0.5f) continue;

                // Пропускаем нейтральные и бонусы (пока)
                if (obs.Category == ObjectCategory.Neutral ||
                    obs.Category == ObjectCategory.Bonus)
                    continue;

                bool sameLane = IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof);

                if (!sameLane) continue;

                if (obs.Category == ObjectCategory.Threat)
                {
                    var step = SelectEvasionTool(obs, i, hamsterOnBottom, hamsterOnRoof, energy);
                    if (step.Action != BotAction.None)
                    {
                        _chain.Add(step);
                        return true;
                    }
                }
                else if (obs.Category == ObjectCategory.Target)
                {
                    // Этап 4: напрыгивание — пока просто прыжок на Target
                    if (energy >= JumpEnergyCost)
                    {
                        _chain.Add(new ChainStep(
                            hamsterOnRoof ? BotAction.RoofJump : BotAction.Jump,
                            i, SafeMargin, JumpEnergyCost,
                            $"Jump on target {obs.Type}"));
                        return true;
                    }
                }
            }

            return false;
        }

        // ──────────────── Классификация ────────────────

        private static ObjectCategory Classify(
            ObstacleTypeEnum type, bool isTopLane, bool isOnRoof,
            bool hamsterOnBottom, bool hamsterOnRoof, float distance)
        {
            // Объекты позади — нейтральные
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
                    // SmallAlive — цель (можно напрыгнуть)
                    return ObjectCategory.Target;

                case ObstacleTypeEnum.bigAlive:
                    if (hamsterOnRoof)
                        return ObjectCategory.Target; // Можно напрыгнуть с крыши
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

        // ──────────────── Выбор инструмента уклонения ────────────────

        private ChainStep SelectEvasionTool(
            ObstacleInfo threat, int index,
            bool hamsterOnBottom, bool hamsterOnRoof, int energy)
        {
            // Порядок: дешёвые инструменты первыми
            switch (threat.Type)
            {
                case ObstacleTypeEnum.bigAlive:
                    // 1. Смена линии (бесплатно)
                    if (IsOtherLaneSafe(threat, hamsterOnBottom))
                        return new ChainStep(BotAction.SwitchLane, index,
                            SafeMargin, 0, "Evade bigAlive: switch lane");
                    // 2. Суперпрыжок (20 энергии)
                    if (energy >= SuperJumpEnergyCost)
                        return new ChainStep(
                            hamsterOnRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                            index, SafeMargin, SuperJumpEnergyCost,
                            "Evade bigAlive: super jump");
                    break;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    // 1. Прыжок на крышу (10 энергии) — только с дороги
                    if (!hamsterOnRoof && energy >= JumpEnergyCost)
                        return new ChainStep(BotAction.Jump, index,
                            SafeMargin, JumpEnergyCost,
                            $"Evade {threat.Type}: jump on roof");
                    // 2. Смена линии
                    if (IsOtherLaneSafe(threat, hamsterOnBottom))
                        return new ChainStep(BotAction.SwitchLane, index,
                            SafeMargin, 0, $"Evade {threat.Type}: switch lane");
                    break;

                case ObstacleTypeEnum.smallNotAliveRoad:
                    // 1. Смена линии
                    if (IsOtherLaneSafe(threat, hamsterOnBottom))
                        return new ChainStep(BotAction.SwitchLane, index,
                            SafeMargin, 0, "Evade smallNotAliveRoad: switch lane");
                    // 2. Прыжок (10 энергии)
                    if (energy >= JumpEnergyCost)
                        return new ChainStep(
                            hamsterOnRoof ? BotAction.RoofJump : BotAction.Jump,
                            index, SafeMargin, JumpEnergyCost,
                            "Evade smallNotAliveRoad: jump over");
                    break;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    if (hamsterOnRoof)
                    {
                        // На крыше — перепрыгнуть
                        if (energy >= JumpEnergyCost)
                            return new ChainStep(BotAction.RoofJump, index,
                                SafeMargin, JumpEnergyCost,
                                "Evade smallNotAliveRoadAndRoof on roof: roof jump");
                    }
                    else
                    {
                        // На дороге — смена линии или прыжок
                        if (IsOtherLaneSafe(threat, hamsterOnBottom))
                            return new ChainStep(BotAction.SwitchLane, index,
                                SafeMargin, 0,
                                "Evade smallNotAliveRoadAndRoof: switch lane");
                        if (energy >= JumpEnergyCost)
                            return new ChainStep(BotAction.Jump, index,
                                SafeMargin, JumpEnergyCost,
                                "Evade smallNotAliveRoadAndRoof: jump over");
                    }
                    break;
            }

            // Нет доступного инструмента
            return new ChainStep(BotAction.None, -1, 0, 0, "No tool available");
        }

        // ──────────────── Вспомогательные ────────────────

        /// <summary>Проверяет, что другая линия безопасна для смены.</summary>
        private bool IsOtherLaneSafe(ObstacleInfo threat, bool hamsterOnBottom)
        {
            // Зона, которую хомяк пройдёт за время смены линии
            float switchTravel = LaneSwitchDuration * Consts.GameSpeedBase;
            float checkFrom = threat.LeftX - switchTravel;
            float checkTo = threat.RightX + switchTravel;
            bool otherLaneIsTop = hamsterOnBottom; // если хомяк снизу, другая линия — верхняя

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.Category == ObjectCategory.Neutral ||
                    obs.Category == ObjectCategory.Bonus)
                    continue;

                // Объект на другой линии? (линия, куда хомяк перейдёт)
                if (obs.IsTopLane != otherLaneIsTop) continue;
                if (obs.IsOnRoof) continue; // На крыше — не мешает на дороге

                // Пересечение по X?
                if (obs.RightX > checkFrom && obs.LeftX < checkTo)
                {
                    // Есть угроза/цель на другой линии в зоне смены
                    if (obs.Category == ObjectCategory.Threat ||
                        obs.Category == ObjectCategory.Target)
                        return false;
                }
            }

            return true;
        }

        /// <summary>Объект на той же линии, что и хомяк?</summary>
        private static bool IsSameLane(bool obsIsTop, bool hamsterOnBottom,
            bool obsOnRoof, bool hamsterOnRoof)
        {
            if (hamsterOnRoof)
                return obsOnRoof && (obsIsTop != hamsterOnBottom);

            // Хомяк на дороге: top = !hamsterOnBottom
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
    }
}
