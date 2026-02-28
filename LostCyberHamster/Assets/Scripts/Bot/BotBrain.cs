using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Реактивная система принятия решений: обрабатывает непосредственные угрозы
    /// (Priority 1-8 из ТЗ). Вызывается когда ближайшая угроза — в зоне быстрой реакции,
    /// либо как fallback, когда BotPlanner не нужен.
    /// </summary>
    public class BotBrain
    {
        private readonly float _reactionWindowSec;
        private readonly float _aggressionLevel;
        private readonly int _ultaClusterThreshold;
        private readonly int _energyConserveThreshold;

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
            var urgentThreat = FindNearestDanger(currentLane);
            if (urgentThreat.HasValue && urgentThreat.Value.TimeToReach < _reactionWindowSec)
            {
                var decision = HandleUrgentThreat(hamster, urgentThreat.Value, otherLane);
                if (decision.Action != BotAction.None)
                    return decision;
            }

            // Priority 4: Ульта
            if (hamster.UltaChargeAmount.Value >= 100)
            {
                int dangerCount = CountDangersInWindow(currentLane, _reactionWindowSec * 2f);
                if (dangerCount >= _ultaClusterThreshold || hamster.Lives.Value <= 1)
                    return BotDecision.Urgent(BotAction.UseUlta,
                        $"ulta ready, {dangerCount} dangers ahead / lives={hamster.Lives.Value}");
            }

            // Priority 5: Напрыгивание на smallAlive для бонусов
            if (hamster.Energy.Value >= 10 && _aggressionLevel > 0.3f)
            {
                var jumpTarget = FindFirstOfType(currentLane, ObstacleTypeEnum.smallAlive,
                    maxTime: _reactionWindowSec * 1.5f);
                if (jumpTarget.HasValue)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"jump on smallAlive for bonus @{jumpTarget.Value.DistanceX:F1}");
            }

            // Priority 5b: Прыжок на крышу bigNotAlive
            if (hamster.Energy.Value >= 10)
            {
                var roofTarget = FindFirstRoofable(currentLane, maxTime: _reactionWindowSec * 1.2f);
                if (roofTarget.HasValue)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"jump on roof {roofTarget.Value.Type} @{roofTarget.Value.DistanceX:F1}");
            }

            // Priority 6: Коллектиблы на другой линии
            var collectible = FindBestCollectible(otherLane);
            if (collectible.HasValue && IsLaneSafe(otherLane))
            {
                return BotDecision.Urgent(BotAction.SwitchLane,
                    $"collect {collectible.Value.Type} on other lane @{collectible.Value.DistanceX:F1}");
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

        // ──────────────── Roof Run ────────────────

        private BotDecision EvaluateRoofRun(Hamster hamster, IReadOnlyList<ThreatInfo> currentLane)
        {
            // На крыше: ищем smallNotAliveRoadAndRoof на крыше впереди
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
            // Во время прыжка можно только SuperJump
            if (!CanSuperJump(hamster.HamsterState.Value))
                return BotDecision.DoNothing("in jump, no super available");

            if (hamster.Energy.Value < 20)
                return BotDecision.DoNothing("in jump, no energy for super");

            // Ищем что-то, что стоит суперпрыжка
            for (int i = 0; i < currentLane.Count; i++)
            {
                var t = currentLane[i];
                if (t.IsDangerous && t.TimeToReach < _reactionWindowSec)
                    return BotDecision.Urgent(BotAction.SuperJump,
                        $"super jump to avoid {t.Type} @{t.DistanceX:F1}");

                if (t.IsRoofable && t.TimeToReach < _reactionWindowSec)
                    return BotDecision.Urgent(BotAction.SuperJump,
                        $"super jump onto roof {t.Type} @{t.DistanceX:F1}");
            }

            return BotDecision.DoNothing("in jump, nothing in range");
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

            // smallNotAlive → перепрыгнуть
            if ((threat.Type == ObstacleTypeEnum.smallNotAliveRoad ||
                 threat.Type == ObstacleTypeEnum.smallNotAliveRoadAndRoof) && energy >= 10)
                return BotDecision.Urgent(BotAction.Jump,
                    $"jump over {threat.Type} @{threat.DistanceX:F1}");

            // bigAlive → попробовать уйти на другую линию
            if (threat.Type == ObstacleTypeEnum.bigAlive)
            {
                if (IsLaneSafe(otherLane))
                    return BotDecision.Urgent(BotAction.SwitchLane,
                        $"dodge bigAlive @{threat.DistanceX:F1}, other lane safe");

                // Другая линия тоже опасна — прыгаем
                if (energy >= 10)
                    return BotDecision.Urgent(BotAction.Jump,
                        $"forced jump, bigAlive @{threat.DistanceX:F1}, both lanes dangerous");
            }

            // Общий fallback: прыгать если есть энергия, иначе сменить линию
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
            // Приоритет: life > crystal > energetic > pizza > coin
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
            // Линия безопасна, если нет опасности в ближайшей зоне реакции
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
                   state == HamsterStateEnum.JumpOnRoofDamage;
        }

        private static bool CanSuperJump(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.Jump ||
                   state == HamsterStateEnum.JumpOver ||
                   state == HamsterStateEnum.JumpOnObstacle ||
                   state == HamsterStateEnum.JumpOnRoof ||
                   state == HamsterStateEnum.JumpDamageForSmallAlive ||
                   state == HamsterStateEnum.JumpDamageForSmallNotAlive ||
                   state == HamsterStateEnum.JumpDamageForBigAlive ||
                   state == HamsterStateEnum.JumpOnRoofDamage;
        }
    }
}
