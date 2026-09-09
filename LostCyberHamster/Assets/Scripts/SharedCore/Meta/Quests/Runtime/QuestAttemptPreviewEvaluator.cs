using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    /// <summary>Проецирует буфер через production-стратегию, не вызывая изменяющий состояние Quest.Handle.</summary>
    internal static class QuestAttemptPreviewEvaluator
    {
        public static QuestAttemptPreview Evaluate(string profileId, long generation, string attemptId,
            Quest quest, IReadOnlyList<ActionCounterQuestEvent> actions, IQuestStrategy strategy)
        {
            // Повторяем условия применимости Quest.Handle, включая запрет action для уникальных уровней.
            if (quest.Type != QuestType.ActionCounter || quest.IsCompleted || quest.IsRewardClaimed ||
                quest.Definition == null || quest.Definition.CountUniqueLevels || quest.TargetAmount <= 0 ||
                string.IsNullOrEmpty(quest.InstanceId))
                return null;

            // Каждый вклад вычисляет та же стратегия, что обрабатывает успешный результат.
            long contribution = 0;
            foreach (var action in actions)
                contribution += Math.Max(0, strategy.CalculateProgress(quest.Definition, action));
            return new QuestAttemptPreview(profileId, generation, attemptId, quest,
                (int)Math.Min(int.MaxValue, contribution));
        }
    }
}
