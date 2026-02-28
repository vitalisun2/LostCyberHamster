using System;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.Learning
{
    /// <summary>
    /// Данные о завершённой игровой сессии бота.
    /// Собираются во время геймплея через подписку на GameEventsManager.
    /// </summary>
    [Serializable]
    public class BotSessionReport
    {
        public string LevelName;
        public BotPlayStyle PlayStyle;

        // ──────────────── Время ────────────────

        /// <summary>Время жизни бота в секундах.</summary>
        public float TimeAlive;

        /// <summary>Момент старта сессии (Time.time).</summary>
        public float SessionStartTime;

        // ──────────────── Жизни ────────────────

        public int LivesAtStart;
        public int LivesAtEnd;
        public int LivesLost;
        public int LivesGained;

        // ──────────────── Энергия ────────────────

        public int EnergySpentTotal;
        public int EnergyGainedTotal;

        // ──────────────── Ресурсы ────────────────

        public int CoinsCollected;
        public int CrystalsCollected;
        public int CoinsAtStart;
        public int CoinsAtEnd;

        // ──────────────── Действия бота ────────────────

        public int JumpsExecuted;
        public int SuperJumpsExecuted;
        public int UltaUsesCount;
        public int LaneSwitches;
        public int EnergyPurchases;
        public int UltaPurchases;

        // ──────────────── Столкновения ────────────────

        public int ObstacleCollisions;
        public int ObstaclesJumpedOver;
        public int ObstaclesJumpedOn;

        // ──────────────── Результат ────────────────

        public bool Won;

        /// <summary>Причины провала, определяемые SessionAnalyzer.</summary>
        public List<FailReason> FailReasons = new();
    }

    /// <summary>
    /// Перечисление причин провала — используются для целевых мутаций в ParameterTuner.
    /// </summary>
    public enum FailReason
    {
        None,
        /// <summary>Умер при энергии &lt; 10.</summary>
        EnergyDepleted,
        /// <summary>Частые столкновения с препятствиями.</summary>
        DiedToBigAlive,
        /// <summary>Мало собранных монет (для BonusHunter).</summary>
        MissedOpportunities,
        /// <summary>Много монет, но умер (для GodMode).</summary>
        UnusedResources,
        /// <summary>Мало использований ульты (для UltaMaster).</summary>
        TooFewUltaUses,
        /// <summary>Слишком агрессивная игра — много столкновений.</summary>
        TooAggressive
    }
}
