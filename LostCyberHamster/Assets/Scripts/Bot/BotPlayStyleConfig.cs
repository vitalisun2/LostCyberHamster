namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Конфигурация стиля игры бота — веса, пороги и параметры поведения.
    /// Создаётся через <see cref="BotPlayStylePresets"/> или вручную.
    /// </summary>
    public class BotPlayStyleConfig
    {
        // ──────────────── Идентификация ────────────────

        /// <summary>Стиль игры, к которому относится конфигурация.</summary>
        public BotPlayStyle Style;

        // ──────────────── Scoring Weights ────────────────

        /// <summary>Вес выживания (жизни) при оценке решений.</summary>
        public float WeightSurvival = 10f;

        /// <summary>Вес сохранения энергии.</summary>
        public float WeightEnergy = 3f;

        /// <summary>Вес сбора бонусов/коллектиблов.</summary>
        public float WeightCollectibles = 2f;

        /// <summary>Вес позиции (нижняя линия = больше контроля).</summary>
        public float WeightPosition = 1f;

        /// <summary>Вес ульты (зарядка и использование).</summary>
        public float WeightUlta = 2f;

        // ──────────────── Реактивное поведение ────────────────

        /// <summary>Агрессивность: 0=осторожный, 1=агрессивный.</summary>
        public float AggressionLevel = 0.7f;

        /// <summary>Окно быстрой реакции (секунды).</summary>
        public float UrgentWindowSec = 0.6f;

        /// <summary>Минимум энергии, ниже которого бот экономит прыжки.</summary>
        public int EnergyConserveThreshold = 30;

        /// <summary>Порог кластера опасностей для активации ульты.</summary>
        public int UltaClusterThreshold = 2;

        /// <summary>Минимум жизней, при котором ульта активируется по одному триггеру.</summary>
        public int UltaEmergencyLives = 1;

        // ──────────────── Покупки ────────────────

        /// <summary>Разрешить покупку энергии за монеты.</summary>
        public bool AllowBuyEnergy = false;

        /// <summary>Покупать энергию, когда energy меньше порога.</summary>
        public int BuyEnergyThreshold = 40;

        /// <summary>Минимум монет для покупки энергии.</summary>
        public int BuyEnergyCoinMinimum = 100;

        /// <summary>Разрешить покупку ульты за монеты.</summary>
        public bool AllowBuyUlta = false;

        /// <summary>Покупать ульту, когда заряд меньше порога (%).</summary>
        public int BuyUltaThreshold = 50;

        /// <summary>Минимум монет для покупки ульты.</summary>
        public int BuyUltaCoinMinimum = 150;

        // ──────────────── Планирование ────────────────

        /// <summary>Включить BotPlanner (просчёт на N шагов вперёд).</summary>
        public bool EnablePlanner = false;

        /// <summary>Глубина дерева поиска.</summary>
        public int PlannerDepth = 3;

        /// <summary>
        /// Создаёт глубокую копию конфигурации.
        /// </summary>
        public BotPlayStyleConfig Clone()
        {
            return (BotPlayStyleConfig)MemberwiseClone();
        }
    }
}
