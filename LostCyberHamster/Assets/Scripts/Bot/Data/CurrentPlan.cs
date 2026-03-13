using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Текущий активный план бота: FIFO-очередь шагов.
    /// Хранит выбранную ChainScorer'ом цепочку и позволяет
    /// инкрементально обновлять хвост без полного пересчёта.
    /// </summary>
    public class CurrentPlan
    {
        public List<ChainStep> Steps = new List<ChainStep>();

        /// <summary>Причина выбора этого плана (для логирования).</summary>
        public string Strategy;

        /// <summary>Первый шаг, ожидающий выполнения. Null если план пуст.</summary>
        public ChainStep Head => Steps.Count > 0 ? Steps[0] : null;

        public bool IsEmpty => Steps.Count == 0;

        /// <summary>
        /// Удаляет завершённые шаги из начала очереди.
        /// </summary>
        public void RemoveCompletedFromHead()
        {
            while (Steps.Count > 0 && Steps[0].Status == ChainStepStatus.Completed)
                Steps.RemoveAt(0);
        }

        /// <summary>
        /// Возвращает шаги после Head (хвост плана) — для keep-tail при пересчёте.
        /// </summary>
        public List<ChainStep> GetTail()
        {
            if (Steps.Count <= 1)
                return new List<ChainStep>();

            return Steps.GetRange(1, Steps.Count - 1);
        }
    }
}
