using System.Collections.Generic;
using System.Text;
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

        private const float SafeMargin = 1.5f;
        private const float LaneSwitchDuration = 0.3f;
        private const float LaneSwitchTravel = LaneSwitchDuration * Consts.GameSpeedBase;
        private const int JumpEnergyCost = 10;
        private const int SuperJumpEnergyCost = 20;
        private const int BuyEnergyPrice = 50;
        private const int BuyEnergyAmount = 100;

        // Приблизительные дальности приземления прыжков (в юнитах сдвига сцены)
        private const float JumpLandingTravel = 3.8f;   // ~1с * GameSpeedBase
        private const float LandingCheckTolerance = 0.8f;

        // JumpOnObstacle (напрыгивание на SmallAlive): отскок длится 1.817с,
        // но цель находится ~0.85с в анимации. Иммунное расстояние после цели:
        // (1.817 - 0.85) * GameSpeedBase ≈ 3.5 юнитов.
        private const float JumpOnBounceTravel = 3.5f;


        // ──────────────── Переиспользуемые буферы ────────────────

        private readonly List<ObstacleInfo> _obstacles = new(32);
        private readonly List<ChainStep> _chain = new(8);
        private readonly StringBuilder _logBuf = new(512);
        private int _decisionId;

        // Decision dedup for logging
        private BotAction _prevLoggedAction;
        private int _prevLoggedTargetIdx = -1;
        private string _prevLoggedStrategy;

        // ──────────────── Публичные результаты ────────────────

        /// <summary>Текущий список отсканированных объектов (readonly view).</summary>
        public IReadOnlyList<ObstacleInfo> Obstacles => _obstacles;

        /// <summary>Текущая построенная цепочка.</summary>
        public IReadOnlyList<ChainStep> Chain => _chain;

        // ══════════════════════════════════════════════
        //  Загрузка снапшота (Этап 2)
        // ══════════════════════════════════════════════

        /// <summary>
        /// Загружает объекты из снимка сцены в внутренний буфер.
        /// Выполняет классификацию объектов с учётом текущего состояния хомяка из snapshot.
        /// Вызывается HamsterBot'ом после получения снимка от SnapshotBuilder.
        /// </summary>
        public void LoadFromSnapshot(BotSceneSnapshot snapshot)
        {
            _obstacles.Clear();

            bool hamsterOnBottom = snapshot.HamsterOnBottom;
            bool hamsterOnRoof   = snapshot.HamsterOnRoof;

            // VisibleObjects уже отсортированы по LeftX в SnapshotBuilder
            foreach (var obs in snapshot.VisibleObjects)
            {
                var category = Classify(obs.Type, obs.IsTopLane, obs.IsOnRoof,
                    hamsterOnBottom, hamsterOnRoof, obs.DistanceToHamster);

                _obstacles.Add(new ObstacleInfo(
                    obs.Type, obs.LeftX, obs.RightX, obs.CenterX,
                    obs.IsTopLane, obs.IsOnRoof,
                    obs.DistanceToHamster, obs.TimeToReach,
                    category, obs.ObstacleRef, obs.StableId));
            }
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
            _decisionId++;

            // ── Этап 6: Ульта ──
            if (ulta >= 100 && TryBuildUltaChain(hamsterOnBottom, hamsterOnRoof, lives))
            {
                LogDecision(hamsterOnBottom, hamsterOnRoof, energy, lives, ulta, "ULTA");
                return true;
            }

            // ── Этап 4: Напрыгивание на Target ──
            if (TryBuildTargetChain(hamsterOnBottom, hamsterOnRoof, energy, hamster))
            {
                LogDecision(hamsterOnBottom, hamsterOnRoof, energy, lives, ulta, "TARGET");
                return true;
            }

            // ── Этап 3: Уклонение от Threat ──
            if (TryBuildEvasionChain(hamsterOnBottom, hamsterOnRoof, energy, hamster))
            {
                LogDecision(hamsterOnBottom, hamsterOnRoof, energy, lives, ulta, "EVASION");
                return true;
            }

            // ── Этап 7: Сбор Bonus (если текущая линия чистая) ──
            if (TryBuildBonusChain(hamsterOnBottom, hamsterOnRoof))
            {
                LogDecision(hamsterOnBottom, hamsterOnRoof, energy, lives, ulta, "BONUS");
                return true;
            }

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
                // JumpOn (SmallAlive): отскок даёт иммунитет на JumpOnBounceTravel после цели.
                bool isJumpOn = obs.Type == ObstacleTypeEnum.smallAlive && !hamsterOnRoof;
                float targetLanding = isJumpOn ? JumpOnBounceTravel : JumpLandingTravel;
                float targetImmune  = isJumpOn ? JumpOnBounceTravel : 0f;

                if (!IsLandingSafe(obs, hamsterOnBottom, hamsterOnRoof, targetLanding, targetImmune))
                {
                    continue; // Небезопасно — пропускаем эту Target
                }

                // Дистанция исполнения: для JumpOn используем JumpLandingTravel
                // как порог входа — точный момент прыжка определяет
                // ShouldDelayJumpOn в HamsterBot через CollisionUtils.
                float execDist = isJumpOn ? JumpLandingTravel : SafeMargin;

                // Проверяем энергию (+ аварийная покупка — Этап 8)
                if (energy < totalEnergyCost)
                {
                    if (TryAddBuyEnergyStep(hamster))
                    {
                        _chain.Add(new ChainStep(jumpAction, i, execDist,
                            jumpCost, $"JumpOn {obs.Type} (after buy energy)"));
                        return true;
                    }
                    continue; // Не хватает энергии и не можем купить
                }

                _chain.Add(new ChainStep(jumpAction, i, execDist,
                    jumpCost, $"JumpOn target {obs.Type}"));
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
                // Уклоняемся от Threat и от Target (которые Target-chain не смог обработать)
                if (obs.Category != ObjectCategory.Threat && obs.Category != ObjectCategory.Target)
                    continue;
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

        /// <summary>
        /// Универсальный подбор инструмента для уклонения от угрозы.
        /// Порядок приоритетов (от дешёвого к дорогому):
        ///   1. Смена линии (бесплатно) — если другая линия безопасна
        ///   2. Прямой прыжок/суперпрыжок — если тип позволяет
        ///   3. Смена линии на линию с преодолеваемыми угрозами (fallback)
        /// </summary>
        private ChainStep SelectEvasionTool(
            ObstacleInfo threat, int index,
            bool hamsterOnBottom, bool hamsterOnRoof, int energy)
        {
            // ─── 1. Смена линии (бесплатно, если другая линия чистая) ───
            if (IsOtherLaneSafe(threat, hamsterOnBottom))
                return new ChainStep(BotAction.SwitchLane, index,
                    SafeMargin, 0, $"Evade {threat.Type}: switch lane");

            // ─── 2. Прямые инструменты на текущей линии ───
            var direct = TryDirectEvasion(threat, index, hamsterOnBottom, hamsterOnRoof, energy);
            if (direct.Action != BotAction.None)
                return direct;

            // ─── 3. Fallback: смена линии, даже если там есть угрозы ───
            // Если все угрозы на другой линии преодолеваемые (jumpable) и у нас
            // хватает энергии хотя бы на один прыжок — лучше сменить линию,
            // чем гарантированно врезаться в текущую угрозу.
            if (IsOtherLaneEvadable(threat, hamsterOnBottom, energy))
                return new ChainStep(BotAction.SwitchLane, index,
                    SafeMargin, 0, $"Evade {threat.Type}: switch to evadable lane");

            return new ChainStep(BotAction.None, -1, 0, 0, "No tool available");
        }

        /// <summary>
        /// Прямые инструменты для конкретного типа угрозы (без смены линии).
        /// </summary>
        private ChainStep TryDirectEvasion(
            ObstacleInfo threat, int index,
            bool hamsterOnBottom, bool hamsterOnRoof, int energy)
        {
            switch (threat.Type)
            {
                // ─── Перепрыгиваемые мелкие ───
                case ObstacleTypeEnum.smallAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                {
                    if (energy >= JumpEnergyCost)
                    {
                        var jumpAct = hamsterOnRoof ? BotAction.RoofJump : BotAction.Jump;
                        if (IsLandingSafe(threat, hamsterOnBottom, hamsterOnRoof, JumpLandingTravel))
                            return new ChainStep(jumpAct, index,
                                SafeMargin, JumpEnergyCost,
                                $"Evade {threat.Type}: jump over");
                    }
                    break;
                }

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                {
                    if (energy >= JumpEnergyCost)
                    {
                        var jumpAct = hamsterOnRoof ? BotAction.RoofJump : BotAction.Jump;
                        if (IsLandingSafe(threat, hamsterOnBottom, hamsterOnRoof, JumpLandingTravel))
                            return new ChainStep(jumpAct, index,
                                SafeMargin, JumpEnergyCost,
                                $"Evade {threat.Type}: jump over");
                    }
                    break;
                }

                // ─── bigAlive: только суперпрыжок перелетает ───
                case ObstacleTypeEnum.bigAlive:
                {
                    if (energy >= SuperJumpEnergyCost)
                        return new ChainStep(
                            hamsterOnRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                            index, SafeMargin, SuperJumpEnergyCost,
                            "Evade bigAlive: super jump");
                    break;
                }

                // ─── bigNotAlive / mediumNotAlive: прыжок на крышу ───
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                {
                    if (!hamsterOnRoof && energy >= JumpEnergyCost)
                    {
                        bool roofClear = !HasRoofObstacle(threat);
                        if (roofClear)
                            return new ChainStep(BotAction.Jump, index,
                                SafeMargin, JumpEnergyCost,
                                $"Evade {threat.Type}: jump on roof");
                    }
                    break;
                }
            }

            return new ChainStep(BotAction.None, -1, 0, 0, "");
        }

        /// <summary>
        /// Проверяет, что другая линия "проходима": все угрозы на ней
        /// имеют доступный инструмент уклонения (прыжок / суперпрыжок).
        /// Это позволяет боту сменить линию, а на следующем пересчёте
        /// построить шаг для преодоления угрозы на новой линии.
        /// </summary>
        private bool IsOtherLaneEvadable(ObstacleInfo threat, bool hamsterOnBottom, int energy)
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
                if (obs.RightX <= checkFrom || obs.LeftX >= checkTo) continue;

                // Для каждой угрозы на другой линии проверяем: есть ли инструмент?
                if (!HasEvasionTool(obs, energy))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Есть ли в принципе инструмент для преодоления данной угрозы?
        /// (не проверяет смену линии — только прямые инструменты)
        /// </summary>
        private static bool HasEvasionTool(ObstacleInfo obs, int energy)
        {
            switch (obs.Type)
            {
                // Перепрыгиваемые обычным прыжком
                case ObstacleTypeEnum.smallAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return energy >= JumpEnergyCost;

                // Только суперпрыжок
                case ObstacleTypeEnum.bigAlive:
                    return energy >= SuperJumpEnergyCost;

                // bigNotAlive/mediumNotAlive — прыжок на крышу (только с дороги)
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return energy >= JumpEnergyCost;

                default:
                    return false;
            }
        }

        // ══════════════════════════════════════════════
        //  Проверки последствий
        // ══════════════════════════════════════════════

        /// <summary>
        /// Безопасно ли приземление после прыжка через/на объект?
        /// Проверяет зону от source до приземления. Параметр immuneRange позволяет
        /// пропустить угрозы в начале зоны (напр. отскок после JumpOn даёт иммунитет).
        /// </summary>
        private bool IsLandingSafe(ObstacleInfo source,
            bool hamsterOnBottom, bool hamsterOnRoof,
            float landingTravel, float immuneRange = 0f)
        {
            float landingX = source.RightX + landingTravel;
            // Начинаем проверку после зоны иммунитета (если есть)
            float checkFrom = source.RightX + immuneRange;
            float checkTo = landingX + LandingCheckTolerance;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.Category != ObjectCategory.Threat) continue;
                if (!IsSameLane(obs.IsTopLane, hamsterOnBottom, obs.IsOnRoof, hamsterOnRoof))
                    continue;

                // Пропускаем сам source-объект
                if (Mathf.Abs(obs.CenterX - source.CenterX) < 0.1f) continue;

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

            var sb = new StringBuilder();
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

        // ══════════════════════════════════════════════
        //  Decision Logging
        // ══════════════════════════════════════════════

        private void LogDecision(bool hamsterOnBottom, bool hamsterOnRoof,
            int energy, int lives, int ulta, string strategy)
        {
            var step = _chain.Count > 0 ? _chain[0] : default;

            // Dedup: не логировать одно и то же решение каждый кадр
            if (step.Action == _prevLoggedAction &&
                step.TargetObstacleIndex == _prevLoggedTargetIdx &&
                strategy == _prevLoggedStrategy)
                return;

            _prevLoggedAction = step.Action;
            _prevLoggedTargetIdx = step.TargetObstacleIndex;
            _prevLoggedStrategy = strategy;

            _logBuf.Clear();
            _logBuf.Append($"[Bot#{_decisionId}] {strategy}: {step.Action}");
            if (step.TargetObstacleIndex >= 0 && step.TargetObstacleIndex < _obstacles.Count)
            {
                var t = _obstacles[step.TargetObstacleIndex];
                _logBuf.Append($" → {t.Type} dist={t.DistanceToHamster:F2}");
            }
            _logBuf.Append($" | lane={( hamsterOnBottom ? "bot" : "top")} roof={hamsterOnRoof}");
            _logBuf.Append($" E={energy} L={lives} U={ulta}");
            if (!string.IsNullOrEmpty(step.Reason))
                _logBuf.Append($" [{step.Reason}]");

            // Кратко — угрозы на текущей линии
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var o = _obstacles[i];
                if (o.DistanceToHamster < -0.5f || o.DistanceToHamster > 8f) continue;
                if (o.Category == ObjectCategory.Neutral) continue;
                _logBuf.Append($"\n  [{i}]{o.Type}({o.Category}) d={o.DistanceToHamster:F1}"
                    + $" {(o.IsTopLane?"T":"B")}{(o.IsOnRoof?"R":"")}");
            }

            DebugManager.DiagLog(_logBuf.ToString());
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
