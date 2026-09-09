using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    /// <summary>Неизменяемая предварительная проекция одного экземпляра квеста; зачёт требует победы.</summary>
    public sealed class QuestAttemptPreview
    {
        public string ProfileId { get; }
        public long Generation { get; }
        public string AttemptId { get; }
        public string QuestId { get; }
        public string InstanceId { get; }
        public QuestCategory Category { get; }
        public string TitleLocalizationKey { get; }
        public IReadOnlyList<string> TitleLocalizationArguments { get; }
        public int CommittedProgress { get; }
        public int AttemptProgress { get; }
        public int ProjectedProgress { get; }
        public int TargetAmount { get; }
        public bool IsConditionReached => ProjectedProgress >= TargetAmount;

        internal QuestAttemptPreview(string profileId, long generation, string attemptId,
            Quest quest, int attemptProgress)
        {
            // Фиксируем адрес и текст без ссылки на изменяемый runtime-квест.
            ProfileId = profileId;
            Generation = generation;
            AttemptId = attemptId;
            QuestId = quest.Id;
            InstanceId = quest.InstanceId;
            Category = quest.Category;
            TitleLocalizationKey = quest.TitleLocalizationKey;
            TitleLocalizationArguments = Array.AsReadOnly((string[])quest.TitleLocalizationArguments.Clone());

            // Проекция ограничена реальной целью; сохранённое состояние остаётся прежним.
            CommittedProgress = quest.CurrentProgress;
            TargetAmount = quest.TargetAmount;
            AttemptProgress = attemptProgress;
            ProjectedProgress = CommittedProgress + Math.Min(attemptProgress,
                Math.Max(0, TargetAmount - CommittedProgress));
        }
    }
}
