using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Хранит состав и состояние текущего набора дневных квестов.
    /// </summary>
    [Serializable]
    public sealed class DailyQuestSetState
    {
        /// <summary>
        /// Локальная дата последней генерации.
        /// </summary>
        public string GenerationDate = string.Empty;

        /// <summary>
        /// Идентификаторы текущих дневных квестов.
        /// </summary>
        public List<string> ActiveQuestIds = new();

        /// <summary>
        /// Идентификаторы квестов последней генерации.
        /// </summary>
        public List<string> LastGeneratedQuestIds = new();

        /// <summary>
        /// Признак получения общей награды текущего набора.
        /// </summary>
        public bool CommonRewardClaimed;
    }
}
