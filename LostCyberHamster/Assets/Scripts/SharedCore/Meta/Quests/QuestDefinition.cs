using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Контентное описание одного квеста.
    /// </summary>
    [Serializable]
    public sealed class QuestDefinition
    {
        /// <summary>
        /// Стабильный идентификатор квеста.
        /// </summary>
        public string Id;

        /// <summary>
        /// Текст квеста для текущего MVP.
        /// </summary>
        public string Title;

        /// <summary>
        /// Широкий тип логики квеста.
        /// </summary>
        public QuestType Type;

        /// <summary>
        /// Действие, которое даёт прогресс счётчику.
        /// </summary>
        public string ActionId;

        /// <summary>
        /// Значение прогресса для завершения.
        /// </summary>
        public int TargetAmount;
    }
}
