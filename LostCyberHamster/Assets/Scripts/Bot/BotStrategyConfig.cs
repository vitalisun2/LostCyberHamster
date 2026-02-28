using Sirenix.OdinInspector;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// ScriptableObject с настройками стратегии бота.
    /// Создаётся через Create > Bot > StrategyConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "BotStrategyConfig", menuName = "Bot/Strategy Config")]
    public class BotStrategyConfig : ScriptableObject
    {
        [Title("Timing")]
        [Tooltip("Минимальный интервал между действиями (сек)")]
        [Range(0.02f, 0.2f)]
        public float ActionCooldown = 0.05f;

        [Tooltip("Дальность сканирования (мировые единицы)")]
        [Range(5f, 30f)]
        public float ScanRange = 15f;

        [Tooltip("Окно быстрой реакции (сек): внутри — BotBrain, снаружи — BotPlanner")]
        [Range(0.2f, 1.5f)]
        public float UrgentWindowSec = 0.6f;

        [Title("Behavior")]
        [Tooltip("Агрессивность: 0 = осторожный (обходит всё), 1 = агрессивный (прыгает на всё)")]
        [Range(0f, 1f)]
        public float AggressionLevel = 0.7f;

        [Tooltip("Порог кластера опасностей для активации ульты")]
        [Range(1, 5)]
        public int UltaClusterThreshold = 2;

        [Tooltip("Минимум энергии, ниже которого бот экономит прыжки")]
        [Range(10, 50)]
        public int EnergyConserveThreshold = 30;

        [Title("Forward Simulation")]
        [Tooltip("Включить BotPlanner (просчёт на 2-3 шага вперёд)")]
        public bool EnablePlanner = false;

        [Tooltip("Глубина дерева поиска (шагов вперёд)")]
        [Range(1, 5)]
        [ShowIf("EnablePlanner")]
        public int PlannerDepth = 3;

        [Tooltip("Макс. количество ветвей на шаг")]
        [Range(3, 10)]
        [ShowIf("EnablePlanner")]
        public int PlannerBranchFactor = 5;

        [Title("Scoring Weights")]
        [Tooltip("Вес выживания (0-10)")]
        [Range(0f, 10f)]
        public float WeightSurvival = 10f;

        [Tooltip("Вес энергии (0-10)")]
        [Range(0f, 10f)]
        public float WeightEnergy = 3f;

        [Tooltip("Вес коллектиблов (0-10)")]
        [Range(0f, 10f)]
        public float WeightCollectibles = 2f;

        [Tooltip("Вес позиции (нижняя линия = контроль) (0-10)")]
        [Range(0f, 10f)]
        public float WeightPosition = 1f;
    }
}
