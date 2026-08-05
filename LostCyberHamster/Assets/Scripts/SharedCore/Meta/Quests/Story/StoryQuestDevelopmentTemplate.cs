using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Описывает контентный шаблон квеста развития без runtime-цели.
    /// </summary>
    [Serializable]
    public sealed class StoryQuestDevelopmentTemplate
    {
        /// <summary>
        /// Стабильный идентификатор шаблона.
        /// </summary>
        public string Id;

        /// <summary>
        /// Ключ локализованного названия квеста.
        /// </summary>
        public string TitleLocalizationKey;

        /// <summary>
        /// Идентификатор состояния игрока для runtime-цели.
        /// </summary>
        public string StateId;

        /// <summary>
        /// Тип награды созданного квеста.
        /// </summary>
        public ResourceType RewardType;

        /// <summary>
        /// Размер награды созданного квеста.
        /// </summary>
        public int RewardAmount;
    }
}
