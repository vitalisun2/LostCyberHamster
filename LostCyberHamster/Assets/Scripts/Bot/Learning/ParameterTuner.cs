using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Bot.Learning
{
    /// <summary>
    /// Мутирует параметры генома: целевые мутации (на основе FailReasons) + случайные (10%).
    /// Все параметры ограничены допустимыми диапазонами.
    /// </summary>
    public static class ParameterTuner
    {
        private static readonly global::System.Random Rng = new();

        // ──────────────── Допустимые диапазоны ────────────────

        private const float WeightMin = 0.5f, WeightMax = 20f;
        private const float AggressionMin = 0.1f, AggressionMax = 1.0f;
        private const float UrgentWindowMin = 0.3f, UrgentWindowMax = 1.2f;
        private const int EnergyThresholdMin = 10, EnergyThresholdMax = 80;
        private const int UltaClusterMin = 1, UltaClusterMax = 5;
        private const int UltaEmergencyMin = 1, UltaEmergencyMax = 3;
        private const int BuyThresholdMin = 10, BuyThresholdMax = 80;
        private const int BuyCoinMinMin = 30, BuyCoinMinMax = 300;

        private const float RandomChance = 0.10f;

        /// <summary>
        /// Создаёт мутированную копию генома на основе fail reasons + случайных мутаций.
        /// </summary>
        public static BotGenome Mutate(BotGenome genome, IReadOnlyList<FailReason> failReasons)
        {
            var config = genome.ToConfig();

            // 1. Целевые мутации
            for (int i = 0; i < failReasons.Count; i++)
                ApplyTargetedMutation(config, failReasons[i]);

            // 2. Случайные мутации (10% шанс на каждый параметр)
            ApplyRandomMutations(config);

            // 3. Clamp
            ClampAll(config);

            // Создаём новый геном
            var mutated = BotGenome.FromConfig(config, genome.LevelName);
            mutated.Generation = genome.Generation + 1;
            mutated.BestFitness = genome.BestFitness;
            mutated.FitnessHistory = new List<float>(genome.FitnessHistory);

            return mutated;
        }

        /// <summary>
        /// Описывает, какие параметры будут затронуты целевой мутацией для данного fail reason.
        /// Используется для отображения в UI.
        /// </summary>
        public static string DescribeMutation(FailReason reason)
        {
            return reason switch
            {
                FailReason.EnergyDepleted     => "EnergyConserveThreshold+5, WeightEnergy+1",
                FailReason.DiedToBigAlive     => "UrgentWindowSec+0.05, WeightSurvival+0.5",
                FailReason.MissedOpportunities => "WeightCollectibles+1, AggressionLevel+0.05",
                FailReason.UnusedResources    => "EnableBuys, BuyThresholds+10",
                FailReason.TooFewUltaUses     => "WeightUlta+1, UltaCluster-1",
                FailReason.TooAggressive      => "AggressionLevel-0.1, WeightSurvival+1",
                _                             => ""
            };
        }

        // ──────────────── Целевые мутации ────────────────

        private static void ApplyTargetedMutation(BotPlayStyleConfig cfg, FailReason reason)
        {
            switch (reason)
            {
                case FailReason.EnergyDepleted:
                    cfg.EnergyConserveThreshold += 5;
                    cfg.WeightEnergy += 1.0f;
                    break;

                case FailReason.DiedToBigAlive:
                    cfg.UrgentWindowSec += 0.05f;
                    cfg.WeightSurvival += 0.5f;
                    break;

                case FailReason.MissedOpportunities:
                    cfg.WeightCollectibles += 1.0f;
                    cfg.AggressionLevel += 0.05f;
                    break;

                case FailReason.UnusedResources:
                    cfg.AllowBuyEnergy = true;
                    cfg.AllowBuyUlta = true;
                    cfg.BuyEnergyThreshold += 10;
                    cfg.BuyUltaThreshold += 10;
                    break;

                case FailReason.TooFewUltaUses:
                    cfg.WeightUlta += 1.0f;
                    cfg.UltaClusterThreshold = Mathf.Max(1, cfg.UltaClusterThreshold - 1);
                    break;

                case FailReason.TooAggressive:
                    cfg.AggressionLevel -= 0.1f;
                    cfg.WeightSurvival += 1.0f;
                    break;
            }
        }

        // ──────────────── Случайные мутации ────────────────

        private static void ApplyRandomMutations(BotPlayStyleConfig cfg)
        {
            if (Roll()) cfg.WeightSurvival += Delta(0.5f);
            if (Roll()) cfg.WeightEnergy += Delta(0.5f);
            if (Roll()) cfg.WeightCollectibles += Delta(0.5f);
            if (Roll()) cfg.WeightPosition += Delta(0.3f);
            if (Roll()) cfg.WeightUlta += Delta(0.5f);
            if (Roll()) cfg.AggressionLevel += Delta(0.05f);
            if (Roll()) cfg.UrgentWindowSec += Delta(0.05f);
            if (Roll()) cfg.EnergyConserveThreshold += IntDelta(5);
            if (Roll()) cfg.UltaClusterThreshold += IntDelta(1);
            if (Roll()) cfg.BuyEnergyThreshold += IntDelta(5);
            if (Roll()) cfg.BuyUltaThreshold += IntDelta(5);
        }

        // ──────────────── Clamp ────────────────

        private static void ClampAll(BotPlayStyleConfig cfg)
        {
            cfg.WeightSurvival = Mathf.Clamp(cfg.WeightSurvival, WeightMin, WeightMax);
            cfg.WeightEnergy = Mathf.Clamp(cfg.WeightEnergy, WeightMin, WeightMax);
            cfg.WeightCollectibles = Mathf.Clamp(cfg.WeightCollectibles, WeightMin, WeightMax);
            cfg.WeightPosition = Mathf.Clamp(cfg.WeightPosition, WeightMin, WeightMax);
            cfg.WeightUlta = Mathf.Clamp(cfg.WeightUlta, WeightMin, WeightMax);
            cfg.AggressionLevel = Mathf.Clamp(cfg.AggressionLevel, AggressionMin, AggressionMax);
            cfg.UrgentWindowSec = Mathf.Clamp(cfg.UrgentWindowSec, UrgentWindowMin, UrgentWindowMax);
            cfg.EnergyConserveThreshold = Mathf.Clamp(cfg.EnergyConserveThreshold, EnergyThresholdMin, EnergyThresholdMax);
            cfg.UltaClusterThreshold = Mathf.Clamp(cfg.UltaClusterThreshold, UltaClusterMin, UltaClusterMax);
            cfg.UltaEmergencyLives = Mathf.Clamp(cfg.UltaEmergencyLives, UltaEmergencyMin, UltaEmergencyMax);
            cfg.BuyEnergyThreshold = Mathf.Clamp(cfg.BuyEnergyThreshold, BuyThresholdMin, BuyThresholdMax);
            cfg.BuyEnergyCoinMinimum = Mathf.Clamp(cfg.BuyEnergyCoinMinimum, BuyCoinMinMin, BuyCoinMinMax);
            cfg.BuyUltaThreshold = Mathf.Clamp(cfg.BuyUltaThreshold, BuyThresholdMin, BuyThresholdMax);
            cfg.BuyUltaCoinMinimum = Mathf.Clamp(cfg.BuyUltaCoinMinimum, BuyCoinMinMin, BuyCoinMinMax);
        }

        // ──────────────── Helpers ────────────────

        private static bool Roll() => Rng.NextDouble() < RandomChance;
        private static float Delta(float max) => (float)(Rng.NextDouble() * 2.0 - 1.0) * max;
        private static int IntDelta(int max) => Rng.Next(-max, max + 1);
    }
}
