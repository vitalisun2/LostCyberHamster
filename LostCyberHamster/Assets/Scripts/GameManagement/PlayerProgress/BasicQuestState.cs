using System;

namespace GameManagement
{
    /// <summary>
    /// Изменяемое состояние одного базового квеста игрока.
    /// </summary>
    [Serializable]
    public sealed class BasicQuestState
    {
        public string QuestId;
        public int CurrentProgress;
        public bool IsCompleted;
    }
}
