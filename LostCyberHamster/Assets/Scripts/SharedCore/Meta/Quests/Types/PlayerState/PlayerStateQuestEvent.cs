using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Сообщает текущее значение постоянного состояния сущности игрока.
    /// </summary>
    public sealed class PlayerStateQuestEvent : QuestEvent
    {
        /// <summary>
        /// Идентификатор состояния.
        /// </summary>
        public string StateId { get; }

        /// <summary>
        /// Идентификатор сущности.
        /// </summary>
        public string EntityId { get; }

        /// <summary>
        /// Текущее значение состояния.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Создаёт событие постоянного состояния сущности игрока.
        /// </summary>
        public PlayerStateQuestEvent(
            string stateId,
            string entityId,
            int value)
        {
            if (string.IsNullOrWhiteSpace(stateId))
            {
                throw new ArgumentException(
                    "Идентификатор состояния не должен быть пустым.",
                    nameof(stateId));
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException(
                    "Идентификатор сущности не должен быть пустым.",
                    nameof(entityId));
            }

            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Значение состояния не должно быть отрицательным.");
            }

            StateId = stateId;
            EntityId = entityId;
            Value = value;
        }
    }
}
