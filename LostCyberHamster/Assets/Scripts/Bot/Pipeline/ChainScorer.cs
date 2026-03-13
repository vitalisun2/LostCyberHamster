using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Вычисляет скоринговую оценку для каждого ChainCandidate и сортирует список по убыванию.
    /// Три критерия в порядке приоритета: безопасность → стоимость → выгода.
    /// </summary>
    public class ChainScorer
    {
        // ──────────────── Веса ────────────────

        /// <summary>Базовый бонус за безопасную цепочку.
        /// Должен превышать CostWeight + максимальный BenefitWeight, чтобы
        /// небезопасные цепочки всегда оценивались ниже безопасных.</summary>
        private const float SafetyBonus = 1000f;

        /// <summary>Максимальный вклад компоненты стоимости (меньше энергии = выше score).</summary>
        private const float CostWeight = 10f;

        /// <summary>Максимальный вклад компоненты выгоды.</summary>
        private const float BenefitWeight = 50f;

        // ──────────────── Нормализация стоимости ────────────────

        /// <summary>Максимально возможная трата энергии за цепочку (5 шагов × 20).</summary>
        private const float MaxPossibleCost = 100f;

        // ──────────────── Веса выгоды ────────────────

        private const float TargetWeight     = 10f;  // убитая цель + монеты + заряд ульты

        // Веса коллектибл по типу (индексируется по CollectiblePriority из ObstacleInfo)
        // CollectiblePriority: life=4, energetic/pizza=3, crystal=2, coin=1, 0=не коллектибл
        private static readonly float[] CollectibleWeightByPriority = { 0f, 1f, 3f, 5f, 8f };

        // ══════════════════════════════════════════════
        //  Публичный API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Заполняет поле Score у каждого кандидата и сортирует список по убыванию Score.
        /// Небезопасные кандидаты (AllStepsSafe == false) получают Score = -1.
        /// </summary>
        public void Score(List<ChainCandidate> candidates)
        {
            foreach (var c in candidates)
            {
                // a) Безопасность (страховка — генератор уже отсекает небезопасные)
                if (!c.AllStepsSafe)
                {
                    c.Score = -1f;
                    continue;
                }

                // b) Стоимость: чем меньше потрачено энергии — тем выше вклад
                float normalizedCost = UnityEngine.Mathf.Clamp01(c.TotalEnergyCost / MaxPossibleCost);
                float costScore = (1f - normalizedCost) * CostWeight;

                // c) Выгода
                float benefit = ComputeBenefit(c);
                float benefitScore = benefit * BenefitWeight / MaxBenefitValue;

                c.Score = SafetyBonus + costScore + benefitScore;
            }

            // Сортировка по убыванию Score
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        }

        // ══════════════════════════════════════════════
        //  Вычисление выгоды
        // ══════════════════════════════════════════════

        /// <summary>Максимальная нормализующая константа для benefit.</summary>
        private const float MaxBenefitValue = 50f;

        /// <summary>
        /// Суммирует выгоду по шагам кандидата:
        /// - TargetWeight за каждую уничтоженную цель
        /// - CollectibleWeight за каждый собранный бонус (по его CollectiblePriority)
        /// </summary>
        private static float ComputeBenefit(ChainCandidate c)
        {
            float total = 0f;

            foreach (var step in c.Steps)
            {
                if (!step.TargetObstacle.HasValue) continue;

                var obs = step.TargetObstacle.Value;

                if (obs.Category == ObjectCategory.Target)
                {
                    total += TargetWeight;
                }
                else if (obs.Category == ObjectCategory.Bonus)
                {
                    int priority = UnityEngine.Mathf.Clamp(obs.CollectiblePriority, 0,
                        CollectibleWeightByPriority.Length - 1);
                    total += CollectibleWeightByPriority[priority];
                }
            }

            return total;
        }
    }
}
