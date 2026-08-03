using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Хранит контентные настройки генерируемых сюжетных квестов.
    /// </summary>
    [Serializable]
    public sealed class StoryQuestGenerationSettings
    {
        /// <summary>
        /// Ключ названия последовательного квеста части суток.
        /// </summary>
        public string PrimaryTitleLocalizationKey;

        /// <summary>
        /// Ключ названия последовательного квеста Night.
        /// </summary>
        public string PrimaryNightTitleLocalizationKey;

        /// <summary>
        /// Тип награды последовательного квеста.
        /// </summary>
        public ResourceType PrimaryRewardType;

        /// <summary>
        /// Размер награды последовательного квеста.
        /// </summary>
        public int PrimaryRewardAmount;

        /// <summary>
        /// Ключ названия квеста мастерства.
        /// </summary>
        public string MasteryTitleLocalizationKey;

        /// <summary>
        /// Тип награды квеста мастерства.
        /// </summary>
        public ResourceType MasteryRewardType;

        /// <summary>
        /// Размер награды квеста мастерства.
        /// </summary>
        public int MasteryRewardAmount;
    }
}
