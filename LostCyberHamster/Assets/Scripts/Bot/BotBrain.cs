using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Реактивная система принятия решений, параметризованная через <see cref="BotPlayStyleConfig"/>.
    /// Приоритеты и пороги определяются текущим стилем игры.
    /// </summary>
    public class BotBrain
    {
        private float _reactionWindowSec;
        private float _aggressionLevel;
        private int _ultaClusterThreshold;
        private int _energyConserveThreshold;
        private int _ultaEmergencyLives;

        // Покупки
        private bool _allowBuyEnergy;
        private int _buyEnergyThreshold;
        private int _buyEnergyCoinMinimum;
        private bool _allowBuyUlta;
        private int _buyUltaThreshold;
        private int _buyUltaCoinMinimum;

        private BotResourceManager _resourceManager;

        /// <summary>
        /// Текущий стиль игры (для логирования).
        /// </summary>
        public BotPlayStyle CurrentStyle { get; private set; }

        public BotBrain(BotPlayStyleConfig config, BotResourceManager resourceManager = null)
        {
            _resourceManager = resourceManager ?? new BotResourceManager();
            ApplyConfig(config);
        }

        /// <summary>
        /// Legacy-конструктор для обратной совместимости (использует Survival-подобные параметры).
        /// </summary>
        public BotBrain(
            float reactionWindowSec = 0.6f,
            float aggressionLevel = 0.7f,
            int ultaClusterThreshold = 2,
            int energyConserveThreshold = 30)
        {
            _reactionWindowSec = reactionWindowSec;
            _aggressionLevel = aggressionLevel;
            _ultaClusterThreshold = ultaClusterThreshold;
            _energyConserveThreshold = energyConserveThreshold;
            _ultaEmergencyLives = 1;
            _resourceManager = new BotResourceManager();
            CurrentStyle = BotPlayStyle.Survival;
        }

        /// <summary>
        /// Применяет новую конфигурацию стиля игры на лету.
        /// </summary>
        public void ApplyConfig(BotPlayStyleConfig config)
        {
            CurrentStyle = config.Style;
            _reactionWindowSec = config.UrgentWindowSec;
            _aggressionLevel = config.AggressionLevel;
            _ultaClusterThreshold = config.UltaClusterThreshold;
            _energyConserveThreshold = config.EnergyConserveThreshold;
            _ultaEmergencyLives = config.UltaEmergencyLives;

            _allowBuyEnergy = config.AllowBuyEnergy;
            _buyEnergyThreshold = config.BuyEnergyThreshold;
            _buyEnergyCoinMinimum = config.BuyEnergyCoinMinimum;
            _allowBuyUlta = config.AllowBuyUlta;
            _buyUltaThreshold = config.BuyUltaThreshold;
            _buyUltaCoinMinimum = config.BuyUltaCoinMinimum;
        }

        /// <summary>
        /// Оценивает ситуацию и возвращает решение.
        /// </summary>
        public BotDecision Evaluate(
            Hamster hamster,
            IReadOnlyList<ThreatInfo> currentLane,
            IReadOnlyList<ThreatInfo> otherLane)
        {
            var state = hamster.HamsterState.Value;

            // Priority 1: Нерабочие состояния
            if (state == HamsterStateEnum.Dead)
                return BotDecision.DoNothing("dead");
            if (hamster.IsDamaged.Value)
                return BotDecision.DoNothing("damaged, waiting");
            if (hamster.IsShifting.Value)
                return BotDecision.DoNothing("shifting lanes");

            // Бот на крыше — другая логика
            if (state == HamsterStateEnum.RoofRun)
                return EvaluateRoofRun(hamster, currentLane);

            // Бот в прыжке — возможен суперпрыжок
            if (IsInJumpState(state))
                return EvaluateWhileJumping(hamster, currentLane);

            // Priority 2: Непосредственная угроза
            // Реагируем с запасом 15% для надёжности прыжка
            var urgentThreat = FindNearestDanger(currentLane);
            if (urgentThreat.HasValue && urgentThreat.Value.TimeToReach < _reactionWindowSec * 1.15f)
            {
                var decision = HandleUrgentThreat(hamster, urgentThreat.Value, otherLane);
                if (decision.Action != BotAction.None)
                    return decision;
            }

            // Priority 3: Покупки (энергия / ульта)
            var purchaseDecision = EvaluatePurchases(hamster);
            if (purchaseDecision.Action != BotAction.None)
                return purchaseDecision;

            // Priority 4: Ульта
            if (hamster.UltaChargeAmount.Value >= 100)
            {
                int dangerCount = CountDangersInWindow(currentLane, _reactionWindowSec * 2f);
                if (dangerCount >= _ultaClusterThreshold || hamster.Lives.Value <= _ultaEmergencyLives)
                    return BotDecision.Urgent(BotAction.UseUlta,
                        $"ulta ready, {dangerCount} dangers ahead / lives={hamster.Lives.Value}");
            }

            // Priority 5: Напрыгивание на smallAlive для бонусов
            if (hamster.Energy.Value >= 10 + _energyConserveThreshold && _aggressionLevel > 0.3f)
            {
                var jumpTarget = FindFirstOfType(currentLane, ObstacleTypeEnum.smallAlive,
                    maxTime: _reactionWindowSec * 0.8f);
                if (jumpTarget.HasValue)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"jump on smallAlive for bonus @{jumpTarget.Value.DistanceX:F1}");
            }

            // Priority 5b: Прыжок на крышу bigNotAlive
            if (hamster.Energy.Value >= 10 + _energyConserveThreshold)
            {
                var roofTarget = FindFirstRoofable(currentLane, maxTime: _reactionWindowSec * 0.8f);
                if (roofTarget.HasValue)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"jump on roof {roofTarget.Value.Type} @{roofTarget.Value.DistanceX:F1}");
            }

            // Priority 6: Коллектиблы на другой линии (только при достаточной агрессивности)
            if (_aggressionLevel > 0.4f)
            {
                var collectible = FindBestCollectible(otherLane);
                if (collectible.HasValue && IsLaneSafe(otherLane))
                {
                    return BotDecision.Urgent(BotAction.SwitchLane,
                        $"collect {collectible.Value.Type} on other lane @{collectible.Value.DistanceX:F1}");
                }
            }

            return BotDecision.DoNothing("all clear");
        }

        /// <summary>
        /// Быстрая проверка: есть ли угроза в зоне быстрой реакции?
        /// Используется HamsterBot для выбора между BotBrain и BotPlanner.
        /// </summary>
        public bool HasUrgentThreat(IReadOnlyList<ThreatInfo> currentLane, float urgentWindowSec)
        {
            var danger = FindNearestDanger(currentLane);
            return danger.HasValue && danger.Value.TimeToReach < urgentWindowSec;
        }

        // ──────────────── Purchases ────────────────

        private BotDecision EvaluatePurchases(Hamster hamster)
        {
            // Покупка энергии: мало энергии + есть монеты
            if (_allowBuyEnergy &&
                hamster.Energy.Value < _buyEnergyThreshold &&
                _resourceManager.CurrentCoins >= _buyEnergyCoinMinimum &&
                _resourceManager.CanBuyEnergy())
            {
                return BotDecision.Urgent(BotAction.BuyEnergy,
                    $"buying energy (e={hamster.Energy.Value}, coins={_resourceManager.CurrentCoins})",
                    0.85f);
            }

            // Покупка ульты: мало заряда + есть монеты
            if (_allowBuyUlta &&
                hamster.UltaChargeAmount.Value < _buyUltaThreshold &&
                _resourceManager.CurrentCoins >= _buyUltaCoinMinimum &&
                _resourceManager.CanBuyUlta())
            {
                return BotDecision.Urgent(BotAction.BuyUlta,
                    $"buying ulta (ulta={hamster.UltaChargeAmount.Value}%, coins={_resourceManager.CurrentCoins})",
                    0.80f);
            }

            return BotDecision.DoNothing();
        }

        // ──────────────── Roof Run ────────────────

        private BotDecision EvaluateRoofRun(Hamster hamster, IReadOnlyList<ThreatInfo> currentLane)
        {
            for (int i = 0; i < currentLane.Count; i++)
            {
                var t = currentLane[i];
                if (t.Type == ObstacleTypeEnum.smallNotAliveRoadAndRoof &&
                    t.TimeToReach < _reactionWindowSec &&
                    hamster.Energy.Value >= 10)
                {
                    return BotDecision.Urgent(BotAction.RoofJump,
                        $"roof obstacle ahead @{t.DistanceX:F1}");
                }
            }

            return BotDecision.DoNothing("roofRun, clear");
        }

        // ──────────────── In-Jump ────────────────

        private BotDecision EvaluateWhileJumping(Hamster hamster, IReadOnlyList<ThreatInfo> currentLane)
        {
            if (!CanSuperJump(hamster.HamsterState.Value))
                return BotDecision.DoNothing("in jump, no super available");

            if (hamster.Energy.Value < 20)
                return BotDecision.DoNothing("in jump, no energy for super");

            // SuperJump ТОЛЬКО для bigAlive (высокий — может задеть в воздухе).
            for (int i = 0; i < currentLane.Count; i++)
            {
                var t = currentLane[i];
                if (t.Type == ObstacleTypeEnum.bigAlive && t.TimeToReach < _reactionWindowSec * 0.5f)
                    return BotDecision.Urgent(BotAction.SuperJump,
                        $"super jump to avoid bigAlive @{t.DistanceX:F1}");
            }

            return BotDecision.DoNothing("in jump, nothing urgent");
        }

        // ──────────────── Urgent Threat Handling ────────────────

        private BotDecision HandleUrgentThreat(
            Hamster hamster, ThreatInfo threat, IReadOnlyList<ThreatInfo> otherLane)
        {
            int energy = hamster.Energy.Value;

            // smallAlive → напрыгнуть (безопасно + бонус)
            if (threat.IsSmallAlive && energy >= 10)
                return BotDecision.Urgent(BotAction.Jump,
                    $"jump on smallAlive @{threat.DistanceX:F1} (bonus)");

            // bigNotAlive/mediumNotAlive → залезть на крышу
            if (threat.IsRoofable && energy >= 10)
                return BotDecision.Urgent(BotAction.Jump,
                    $"jump on roof {threat.Type} @{threat.DistanceX:F1}");

            // smallNotAliveRoadAndRoof → не перепрыгнуть! Смена полосы обязательна.
            if (threat.Type == ObstacleTypeEnum.smallNotAliveRoadAndRoof)
            {
                if (IsLaneSafe(otherLane))
                    return BotDecision.Urgent(BotAction.SwitchLane,
                        $"dodge smallNotAliveRoadAndRoof @{threat.DistanceX:F1}, switch lane");

                // Если другая линия тоже опасна — прыжок как крайний вариант
                if (energy >= 10)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"forced jump over roadAndRoof @{threat.DistanceX:F1}, no safe lane");

                return BotDecision.DoNothing("roadAndRoof, no options");
            }

            // smallNotAliveRoad → перепрыгнуть (обычный прыжок работает)
            if (threat.Type == ObstacleTypeEnum.smallNotAliveRoad && energy >= 10)
                return BotDecision.Urgent(BotAction.Jump,
                    $"jump over smallNotAliveRoad @{threat.DistanceX:F1}");

            // bigAlive → попробовать уйти на другую линию
            if (threat.Type == ObstacleTypeEnum.bigAlive)
            {
                if (IsLaneSafe(otherLane))
                    return BotDecision.Urgent(BotAction.SwitchLane,
                        $"dodge bigAlive @{threat.DistanceX:F1}, other lane safe");

                if (energy >= 10)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"forced jump, bigAlive @{threat.DistanceX:F1}, both lanes dangerous");
            }

            // Общий fallback
            if (energy >= 10)
                return BotDecision.Urgent(BotAction.Jump,
                    $"emergency jump @{threat.DistanceX:F1}");

            if (IsLaneSafe(otherLane))
                return BotDecision.Urgent(BotAction.SwitchLane,
                    $"emergency lane switch, no energy");

            return BotDecision.DoNothing("danger but no options");
        }

        // ──────────────── Helpers ────────────────

        private static ThreatInfo? FindNearestDanger(IReadOnlyList<ThreatInfo> lane)
        {
            ThreatInfo? nearest = null;
            for (int i = 0; i < lane.Count; i++)
            {
                if (!lane[i].IsDangerous) continue;
                if (!nearest.HasValue || lane[i].TimeToReach < nearest.Value.TimeToReach)
                    nearest = lane[i];
            }
            return nearest;
        }

        private static ThreatInfo? FindFirstOfType(
            IReadOnlyList<ThreatInfo> lane, ObstacleTypeEnum type, float maxTime)
        {
            for (int i = 0; i < lane.Count; i++)
            {
                if (lane[i].Type == type && lane[i].TimeToReach <= maxTime)
                    return lane[i];
            }
            return null;
        }

        private static ThreatInfo? FindFirstRoofable(IReadOnlyList<ThreatInfo> lane, float maxTime)
        {
            for (int i = 0; i < lane.Count; i++)
            {
                if (lane[i].IsRoofable && lane[i].TimeToReach <= maxTime)
                    return lane[i];
            }
            return null;
        }

        private static ThreatInfo? FindBestCollectible(IReadOnlyList<ThreatInfo> lane)
        {
            ThreatInfo? best = null;
            int bestPriority = -1;

            for (int i = 0; i < lane.Count; i++)
            {
                if (!lane[i].IsCollectable) continue;
                int priority = GetCollectiblePriority(lane[i].Type);
                if (priority > bestPriority)
                {
                    best = lane[i];
                    bestPriority = priority;
                }
            }
            return best;
        }

        private static int GetCollectiblePriority(ObstacleTypeEnum type)
        {
            return type switch
            {
                ObstacleTypeEnum.collectableLife => 5,
                ObstacleTypeEnum.collectableCrystal => 4,
                ObstacleTypeEnum.collectableEnergetic => 3,
                ObstacleTypeEnum.collectablePizza => 2,
                ObstacleTypeEnum.collectableCoin => 1,
                _ => 0
            };
        }

        private static int CountDangersInWindow(IReadOnlyList<ThreatInfo> lane, float windowSec)
        {
            int count = 0;
            for (int i = 0; i < lane.Count; i++)
            {
                if (lane[i].IsDangerous && lane[i].TimeToReach <= windowSec)
                    count++;
            }
            return count;
        }

        private bool IsLaneSafe(IReadOnlyList<ThreatInfo> lane)
        {
            for (int i = 0; i < lane.Count; i++)
            {
                if (lane[i].IsDangerous && lane[i].TimeToReach < _reactionWindowSec)
                    return false;
            }
            return true;
        }

        private static bool IsInJumpState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.Jump ||
                   state == HamsterStateEnum.JumpOver ||
                   state == HamsterStateEnum.JumpOnObstacle ||
                   state == HamsterStateEnum.JumpOnRoof ||
                   state == HamsterStateEnum.JumpDamageForSmallNotAlive ||
                   state == HamsterStateEnum.JumpDamageForSmallAlive ||
                   state == HamsterStateEnum.JumpDamageForBigAlive ||
                   state == HamsterStateEnum.JumpOnRoofDamage ||
                   state == HamsterStateEnum.JumpOnObstacleFromRoof ||
                   state == HamsterStateEnum.JumpFromRoof ||
                   state == HamsterStateEnum.JumpFromRoofDamage ||
                   state == HamsterStateEnum.RoofJump ||
                   state == HamsterStateEnum.RoofJumpDamage ||
                   state == HamsterStateEnum.SuperJump ||
                   state == HamsterStateEnum.SuperJumpDamage ||
                   state == HamsterStateEnum.SuperJumpOver ||
                   state == HamsterStateEnum.SuperJumpOnObstacle ||
                   state == HamsterStateEnum.SuperJumpOnRoof ||
                   state == HamsterStateEnum.SuperJumpOnRoofDamage ||
                   state == HamsterStateEnum.SuperRoofJump ||
                   state == HamsterStateEnum.SuperJumpFromRoof ||
                   state == HamsterStateEnum.SuperJumpOnObstacleFromRoof ||
                   state == HamsterStateEnum.SuperRoofJumpDamage ||
                   state == HamsterStateEnum.SuperJumpFromRoofDamage;
        }

        private static bool CanSuperJump(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.Jump ||
                   state == HamsterStateEnum.RoofJump;
        }
    }
}
