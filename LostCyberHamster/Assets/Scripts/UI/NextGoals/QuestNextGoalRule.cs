using System.Collections.Generic;
using System.Linq;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Предлагает существующее задание, когда его прогресс достиг настроенного порога.</summary>
    internal sealed class QuestNextGoalRule : INextGoalRule
    {
        public NextGoalKind Kind => NextGoalKind.Quest;

        public void Collect(List<NextGoalCandidate> candidates, NextGoalConfiguration configuration)
        {
            foreach (var quest in QuestManager.StoryQuests.Concat(QuestManager.DailyQuests))
            {
                if (quest.Definition == null || quest.IsCompleted || quest.TargetAmount <= 0) continue;
                float progress = (float)quest.CurrentProgress / quest.TargetAmount;
                if (progress < configuration.questNearCompletionRatio) continue;
                candidates.Add(new NextGoalCandidate(Kind, NextGoalAction.Quest, quest.InstanceId,
                    NextGoalText.QuestTitle(quest), NextGoalText.Get("next_goal_quest_progress", quest.CurrentProgress, quest.TargetAmount),
                    NextGoalText.Get("first_session_open_quests"), ScreenEnum.QuestsScreen, progress));
            }
        }
    }
}
