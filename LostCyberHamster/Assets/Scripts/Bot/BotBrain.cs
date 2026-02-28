using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Реактивная система решений для edge-cases, которые нельзя планировать:
    /// dead, damaged, shifting, roof-run, in-jump, purchases, ulta.
    /// Всё остальное (какое действие выполнить и когда) решает <see cref="Planning.BotPlanner"/>.
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

        /// <summary>Текущий стиль игры (для логирования).</summary>
        public BotPlayStyle CurrentStyle { get; private set; }

        public BotBrain(BotPlayStyleConfig config, BotResourceManager resourceManager = null,
            BotJumpPredictor jumpPredictor = null)
        {
            _resourceManager = resourceManager ?? new BotResourceManager();
            ApplyConfig(config);
        }

        /// <summary>
        /// Legacy-конструктор для обратной совместимости.
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
        /// Проверяет immediate edge-cases: dead, damaged, shifting, roof-run, in-jump,
        /// purchases, ulta. Если ни один не сработал — возвращает None (решение за планнером).
        /// </summary>
        public BotDecision EvaluateImmediate(
            Hamster hamster,
            IReadOnlyList<ThreatInfo> currentLane,
            IReadOnlyList<ThreatInfo> otherLane)
        {
            var state = hamster.HamsterState.Value;

            // P1: Нерабочие состояния — бот не может действовать
            if (state == HamsterStateEnum.Dead)
                return BotDecision.DoNothing("dead");
            if (hamster.IsDamaged.Value)
                return BotDecision.DoNothing("damaged, waiting");
            if (hamster.IsShifting.Value)
                return BotDecision.DoNothing("shifting lanes");

            // P2: На крыше — прыжок если smallNotAliveRoadAndRoof
            if (state == HamsterStateEnum.RoofRun)
                return EvaluateRoofRun(hamster, currentLane);

            // P3: В прыжке — SuperJump для bigAlive
            if (IsInJumpState(state))
                return EvaluateWhileJumping(hamster, currentLane);

            // P4: Покупки (энергия / ульта)
            var purchaseDecision = EvaluatePurchases(hamster);
            if (purchaseDecision.Action != BotAction.None)
                return purchaseDecision;

            // P5: Ульта (при кластере опасностей или мало жизней)
            if (hamster.UltaChargeAmount.Value >= 100)
            {
                int dangerCount = CountDangersInWindow(currentLane, _reactionWindowSec * 2f);
                if (dangerCount >= _ultaClusterThreshold || hamster.Lives.Value <= _ultaEmergencyLives)
                    return BotDecision.Urgent(BotAction.UseUlta,
                        $"ulta ready, {dangerCount} dangers ahead / lives={hamster.Lives.Value}");
            }

            // Всё остальное решает BotPlanner
            return BotDecision.DoNothing();
        }

        // ──────────────── Purchases ────────────────

        private BotDecision EvaluatePurchases(Hamster hamster)
        {
            if (_allowBuyEnergy &&
                hamster.Energy.Value < _buyEnergyThreshold &&
                _resourceManager.CurrentCoins >= _buyEnergyCoinMinimum &&
                _resourceManager.CanBuyEnergy())
            {
                return BotDecision.Urgent(BotAction.BuyEnergy,
                    $"buying energy (e={hamster.Energy.Value}, coins={_resourceManager.CurrentCoins})",
                    0.85f);
            }

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

            for (int i = 0; i < currentLane.Count; i++)
            {
                var t = currentLane[i];
                if (t.Type == ObstacleTypeEnum.bigAlive && t.TimeToReach < _reactionWindowSec * 0.5f)
                    return BotDecision.Urgent(BotAction.SuperJump,
                        $"super jump to avoid bigAlive @{t.DistanceX:F1}");
            }

            return BotDecision.DoNothing("in jump, nothing urgent");
        }

        // ──────────────── Helpers ────────────────

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
