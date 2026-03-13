using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Одна кандидатная цепочка действий, сгенерированная ChainGenerator'ом.
    /// ChainScorer оценивает всех кандидатов и выбирает лучшего.
    /// </summary>
    public class ChainCandidate
    {
        public List<ChainStep> Steps = new List<ChainStep>();

        /// <summary>Проецируемое состояние после выполнения всех шагов этой цепочки.</summary>
        public ProjectedState FinalState;

        /// <summary>Суммарная стоимость всех шагов в энергии.</summary>
        public int TotalEnergyCost;

        /// <summary>Все шаги цепочки безопасны (нет неизбежных столкновений).</summary>
        public bool AllStepsSafe;

        /// <summary>Количество Target-объектов, уничтожаемых этой цепочкой.</summary>
        public int TargetsDestroyed;

        /// <summary>Количество Bonus-объектов, собираемых этой цепочкой.</summary>
        public int CollectiblesGathered;

        /// <summary>Итоговая оценка от ChainScorer (выше = лучше).</summary>
        public float Score;
    }
}
