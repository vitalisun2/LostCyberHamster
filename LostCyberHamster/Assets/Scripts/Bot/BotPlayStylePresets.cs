namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Фабрика предустановленных конфигураций для каждого стиля игры.
    /// </summary>
    public static class BotPlayStylePresets
    {
        /// <summary>
        /// Возвращает предустановленную конфигурацию для указанного стиля.
        /// </summary>
        public static BotPlayStyleConfig Get(BotPlayStyle style)
        {
            return style switch
            {
                BotPlayStyle.Survival     => CreateSurvival(),
                BotPlayStyle.ThreeStars   => CreateThreeStars(),
                BotPlayStyle.BonusHunter  => CreateBonusHunter(),
                BotPlayStyle.Perfectionist => CreatePerfectionist(),
                BotPlayStyle.UltaMaster   => CreateUltaMaster(),
                BotPlayStyle.GodMode      => CreateGodMode(),
                _ => CreateSurvival()
            };
        }

        private static BotPlayStyleConfig CreateSurvival()
        {
            return new BotPlayStyleConfig
            {
                Style = BotPlayStyle.Survival,
                // Максимальный приоритет выживания
                WeightSurvival = 10f,
                WeightEnergy = 5f,
                WeightCollectibles = 0.5f,
                WeightPosition = 2f,
                WeightUlta = 1f,
                // Осторожный
                AggressionLevel = 0.3f,
                UrgentWindowSec = 0.6f,
                EnergyConserveThreshold = 40,
                UltaClusterThreshold = 2,
                UltaEmergencyLives = 1,
                // Покупки выключены
                AllowBuyEnergy = false,
                AllowBuyUlta = false,
                // Планирование выключено
                EnablePlanner = false
            };
        }

        private static BotPlayStyleConfig CreateThreeStars()
        {
            return new BotPlayStyleConfig
            {
                Style = BotPlayStyle.ThreeStars,
                // Ещё выше приоритет выживания
                WeightSurvival = 15f,
                WeightEnergy = 4f,
                WeightCollectibles = 1f,
                WeightPosition = 3f,
                WeightUlta = 2f,
                // Очень осторожный
                AggressionLevel = 0.2f,
                UrgentWindowSec = 0.8f,
                EnergyConserveThreshold = 50,
                UltaClusterThreshold = 2,
                UltaEmergencyLives = 2, // Ульта при ≤2 жизнях
                // Без покупок
                AllowBuyEnergy = false,
                AllowBuyUlta = false,
                EnablePlanner = false
            };
        }

        private static BotPlayStyleConfig CreateBonusHunter()
        {
            return new BotPlayStyleConfig
            {
                Style = BotPlayStyle.BonusHunter,
                // Бонусы в приоритете
                WeightSurvival = 5f,
                WeightEnergy = 2f,
                WeightCollectibles = 10f,
                WeightPosition = 0.5f,
                WeightUlta = 3f,
                // Агрессивный
                AggressionLevel = 0.9f,
                UrgentWindowSec = 0.6f,
                EnergyConserveThreshold = 20,
                UltaClusterThreshold = 1,
                UltaEmergencyLives = 1,
                // Покупка энергии разрешена
                AllowBuyEnergy = true,
                BuyEnergyThreshold = 30,
                BuyEnergyCoinMinimum = 100,
                AllowBuyUlta = false,
                EnablePlanner = false
            };
        }

        private static BotPlayStyleConfig CreatePerfectionist()
        {
            return new BotPlayStyleConfig
            {
                Style = BotPlayStyle.Perfectionist,
                // Баланс выживания и бонусов
                WeightSurvival = 12f,
                WeightEnergy = 3f,
                WeightCollectibles = 8f,
                WeightPosition = 2f,
                WeightUlta = 3f,
                // Сбалансированный
                AggressionLevel = 0.6f,
                UrgentWindowSec = 0.7f,
                EnergyConserveThreshold = 35,
                UltaClusterThreshold = 2,
                UltaEmergencyLives = 2,
                // Без покупок
                AllowBuyEnergy = false,
                AllowBuyUlta = false,
                EnablePlanner = false
            };
        }

        private static BotPlayStyleConfig CreateUltaMaster()
        {
            return new BotPlayStyleConfig
            {
                Style = BotPlayStyle.UltaMaster,
                // Ульта в приоритете
                WeightSurvival = 8f,
                WeightEnergy = 2f,
                WeightCollectibles = 5f,
                WeightPosition = 1f,
                WeightUlta = 10f,
                // Агрессивный (прыжки на smallAlive для зарядки)
                AggressionLevel = 0.8f,
                UrgentWindowSec = 0.6f,
                EnergyConserveThreshold = 25,
                UltaClusterThreshold = 1, // Использовать сразу как готова
                UltaEmergencyLives = 3, // Ульта при любых жизнях
                // Покупка ульты разрешена
                AllowBuyEnergy = false,
                AllowBuyUlta = true,
                BuyUltaThreshold = 50,
                BuyUltaCoinMinimum = 150,
                EnablePlanner = false
            };
        }

        private static BotPlayStyleConfig CreateGodMode()
        {
            return new BotPlayStyleConfig
            {
                Style = BotPlayStyle.GodMode,
                // Всё по максимуму
                WeightSurvival = 12f,
                WeightEnergy = 4f,
                WeightCollectibles = 9f,
                WeightPosition = 2f,
                WeightUlta = 7f,
                // Сбалансированно-агрессивный
                AggressionLevel = 0.7f,
                UrgentWindowSec = 0.7f,
                EnergyConserveThreshold = 30,
                UltaClusterThreshold = 1,
                UltaEmergencyLives = 2,
                // Все покупки разрешены
                AllowBuyEnergy = true,
                BuyEnergyThreshold = 40,
                BuyEnergyCoinMinimum = 100,
                AllowBuyUlta = true,
                BuyUltaThreshold = 70,
                BuyUltaCoinMinimum = 150,
                // Планирование включено
                EnablePlanner = true,
                PlannerDepth = 3
            };
        }
    }
}
