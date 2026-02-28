using System;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.Learning
{
    /// <summary>
    /// Сериализуемый геном бота — параметры BotPlayStyleConfig + метаданные обучения.
    /// Хранится в JSON через GenomeManager.
    /// </summary>
    [Serializable]
    public class BotGenome
    {
        // ──────────────── Метаданные ────────────────

        public string LevelName;
        public string PlayStyle;
        public int Generation;
        public float BestFitness;
        public float LastFitness;

        // ──────────────── Scoring Weights ────────────────

        public float WeightSurvival;
        public float WeightEnergy;
        public float WeightCollectibles;
        public float WeightPosition;
        public float WeightUlta;

        // ──────────────── Реактивное поведение ────────────────

        public float AggressionLevel;
        public float UrgentWindowSec;
        public int EnergyConserveThreshold;
        public int UltaClusterThreshold;
        public int UltaEmergencyLives;

        // ──────────────── Покупки ────────────────

        public bool AllowBuyEnergy;
        public int BuyEnergyThreshold;
        public int BuyEnergyCoinMinimum;
        public bool AllowBuyUlta;
        public int BuyUltaThreshold;
        public int BuyUltaCoinMinimum;

        // ──────────────── История ────────────────

        /// <summary>Fitness по поколениям (для графика эволюции).</summary>
        public List<float> FitnessHistory = new();

        // ──────────────── Factory ────────────────

        /// <summary>Создаёт геном из конфигурации стиля.</summary>
        public static BotGenome FromConfig(BotPlayStyleConfig config, string levelName)
        {
            return new BotGenome
            {
                LevelName = levelName,
                PlayStyle = config.Style.ToString(),
                Generation = 0,
                BestFitness = 0f,
                LastFitness = 0f,

                WeightSurvival = config.WeightSurvival,
                WeightEnergy = config.WeightEnergy,
                WeightCollectibles = config.WeightCollectibles,
                WeightPosition = config.WeightPosition,
                WeightUlta = config.WeightUlta,

                AggressionLevel = config.AggressionLevel,
                UrgentWindowSec = config.UrgentWindowSec,
                EnergyConserveThreshold = config.EnergyConserveThreshold,
                UltaClusterThreshold = config.UltaClusterThreshold,
                UltaEmergencyLives = config.UltaEmergencyLives,

                AllowBuyEnergy = config.AllowBuyEnergy,
                BuyEnergyThreshold = config.BuyEnergyThreshold,
                BuyEnergyCoinMinimum = config.BuyEnergyCoinMinimum,
                AllowBuyUlta = config.AllowBuyUlta,
                BuyUltaThreshold = config.BuyUltaThreshold,
                BuyUltaCoinMinimum = config.BuyUltaCoinMinimum,
            };
        }

        /// <summary>Конвертирует геном обратно в BotPlayStyleConfig.</summary>
        public BotPlayStyleConfig ToConfig()
        {
            var style = BotPlayStyle.Survival;
            if (Enum.TryParse<BotPlayStyle>(PlayStyle, out var parsed))
                style = parsed;

            return new BotPlayStyleConfig
            {
                Style = style,
                WeightSurvival = WeightSurvival,
                WeightEnergy = WeightEnergy,
                WeightCollectibles = WeightCollectibles,
                WeightPosition = WeightPosition,
                WeightUlta = WeightUlta,

                AggressionLevel = AggressionLevel,
                UrgentWindowSec = UrgentWindowSec,
                EnergyConserveThreshold = EnergyConserveThreshold,
                UltaClusterThreshold = UltaClusterThreshold,
                UltaEmergencyLives = UltaEmergencyLives,

                AllowBuyEnergy = AllowBuyEnergy,
                BuyEnergyThreshold = BuyEnergyThreshold,
                BuyEnergyCoinMinimum = BuyEnergyCoinMinimum,
                AllowBuyUlta = AllowBuyUlta,
                BuyUltaThreshold = BuyUltaThreshold,
                BuyUltaCoinMinimum = BuyUltaCoinMinimum,

                EnablePlanner = false,
                PlannerDepth = 3,
            };
        }
    }
}
