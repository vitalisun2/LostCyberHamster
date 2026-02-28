namespace Assets.Scripts.Bot.Learning
{
    /// <summary>
    /// Анализирует завершённую игровую сессию и вычисляет Fitness Score.
    /// Формула зависит от текущего PlayStyle. Также определяет FailReasons для целевых мутаций.
    /// </summary>
    public static class SessionAnalyzer
    {
        /// <summary>
        /// Вычисляет итоговый Fitness Score и заполняет FailReasons в отчёте.
        /// </summary>
        public static float Evaluate(BotSessionReport report)
        {
            AnalyzeFailures(report);

            return report.PlayStyle switch
            {
                BotPlayStyle.Survival      => EvaluateSurvival(report),
                BotPlayStyle.ThreeStars    => EvaluateThreeStars(report),
                BotPlayStyle.BonusHunter   => EvaluateBonusHunter(report),
                BotPlayStyle.Perfectionist => EvaluatePerfectionist(report),
                BotPlayStyle.UltaMaster    => EvaluateUltaMaster(report),
                BotPlayStyle.GodMode       => EvaluateGodMode(report),
                _                          => EvaluateSurvival(report)
            };
        }

        // ──────────────── Fitness по стилям ────────────────

        private static float EvaluateSurvival(BotSessionReport r)
        {
            // Survival: главное — прожить дольше, сохранить жизни
            return r.TimeAlive * 10f
                 + r.LivesAtEnd * 1000f
                 - r.ObstacleCollisions * 300f
                 + r.ObstaclesJumpedOver * 50f;
        }

        private static float EvaluateThreeStars(BotSessionReport r)
        {
            // ThreeStars: 3 жизни = 3 звезды, минимум столкновений
            return r.LivesAtEnd * 1000f
                 + r.TimeAlive * 10f
                 - r.ObstacleCollisions * 500f;
        }

        private static float EvaluateBonusHunter(BotSessionReport r)
        {
            // BonusHunter: монеты, кристаллы, прыжки на препятствия
            return r.CoinsCollected * 100f
                 + r.CrystalsCollected * 500f
                 + r.LivesAtEnd * 100f
                 + r.ObstaclesJumpedOn * 200f;
        }

        private static float EvaluatePerfectionist(BotSessionReport r)
        {
            // Perfectionist: сбалансированная оценка всего
            return r.LivesAtEnd * 500f
                 + r.TimeAlive * 5f
                 + r.CoinsCollected * 50f
                 + r.CrystalsCollected * 200f
                 + r.ObstaclesJumpedOver * 30f
                 + r.ObstaclesJumpedOn * 100f
                 - r.ObstacleCollisions * 400f;
        }

        private static float EvaluateUltaMaster(BotSessionReport r)
        {
            // UltaMaster: использования ульты, жизни
            return r.UltaUsesCount * 500f
                 + r.LivesAtEnd * 200f
                 + r.TimeAlive * 5f
                 - r.ObstacleCollisions * 200f;
        }

        private static float EvaluateGodMode(BotSessionReport r)
        {
            // GodMode: всё на максимум + использование покупок
            return r.LivesAtEnd * 800f
                 + r.TimeAlive * 10f
                 + r.CoinsCollected * 80f
                 + r.CrystalsCollected * 400f
                 + r.UltaUsesCount * 300f
                 + r.EnergyPurchases * 200f
                 + r.UltaPurchases * 200f
                 - r.ObstacleCollisions * 300f;
        }

        // ──────────────── Анализ причин провала ────────────────

        private static void AnalyzeFailures(BotSessionReport r)
        {
            r.FailReasons.Clear();

            // EnergyDepleted: потратил всю энергию и умер
            if (!r.Won && r.EnergySpentTotal > r.EnergyGainedTotal + 50)
                r.FailReasons.Add(FailReason.EnergyDepleted);

            // TooAggressive: 3+ столкновений
            if (r.ObstacleCollisions >= 3)
                r.FailReasons.Add(FailReason.TooAggressive);

            // MissedOpportunities: для BonusHunter — мало монет
            if (r.PlayStyle == BotPlayStyle.BonusHunter && r.CoinsCollected < 20)
                r.FailReasons.Add(FailReason.MissedOpportunities);

            // UnusedResources: для GodMode — умер с кучей монет
            if (r.PlayStyle == BotPlayStyle.GodMode && !r.Won && r.CoinsAtEnd >= 300)
                r.FailReasons.Add(FailReason.UnusedResources);

            // TooFewUltaUses: для UltaMaster
            if (r.PlayStyle == BotPlayStyle.UltaMaster && r.UltaUsesCount < 1)
                r.FailReasons.Add(FailReason.TooFewUltaUses);
        }
    }
}
