using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Сообщает о выполненном игроком действии.
    /// </summary>
    public sealed class ActionCounterQuestEvent : QuestEvent
    {
        /// <summary>
        /// Идентификатор выполненного действия.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// Сколько раз действие выполнено.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Создаёт событие выполненного действия.
        /// </summary>
        public ActionCounterQuestEvent(string actionId, int count)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException(
                    "Идентификатор действия не должен быть пустым.",
                    nameof(actionId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    count,
                    "Количество действий должно быть положительным.");
            }

            ActionId = actionId;
            Count = count;
        }
    }
}
