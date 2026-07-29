using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Изменяемое состояние одного квеста игрока.
    /// </summary>
    [Serializable]
    public sealed class QuestState
    {
        /// <summary>
        /// Идентификатор определения квеста.
        /// </summary>
        public string QuestId;

        /// <summary>
        /// Текущий прогресс.
        /// </summary>
        public int CurrentProgress;

        /// <summary>
        /// Признак достижения цели.
        /// </summary>
        public bool IsCompleted;
    }
}
