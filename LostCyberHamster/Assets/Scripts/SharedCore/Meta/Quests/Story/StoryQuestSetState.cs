using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Хранит суточное состояние двух активных сюжетных квестов.
    /// </summary>
    [Serializable]
    public sealed class StoryQuestSetState
    {
        /// <summary>
        /// Дата последней суточной ротации завершённых квестов.
        /// </summary>
        public string GenerationDate = string.Empty;

        /// <summary>
        /// Идентификатор последовательного квеста по прогрессу уровней.
        /// </summary>
        public string ActivePrimaryQuestId = string.Empty;

        /// <summary>
        /// Идентификатор квеста мастерства или развития персонажа.
        /// </summary>
        public string ActiveSecondaryQuestId = string.Empty;
    }
}
