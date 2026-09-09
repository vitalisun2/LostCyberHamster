using System.Collections.Generic;
using System.Linq;
using Vues.GameCore;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает уже заработанные награды через их действующих владельцев.</summary>
    internal sealed class RewardNextGoalRule : INextGoalRule
    {
        public NextGoalKind Kind => NextGoalKind.Reward;

        public void Collect(List<NextGoalCandidate> candidates, NextGoalConfiguration configuration)
        {
            // Экземпляр задания отличает награды одинакового типа в разные дни.
            foreach (var quest in QuestManager.StoryQuests.Concat(QuestManager.DailyQuests))
                if (quest.Definition != null && quest.CanClaimReward)
                    candidates.Add(new NextGoalCandidate(Kind, NextGoalAction.Quest, quest.InstanceId,
                        NextGoalText.QuestTitle(quest), NextGoalText.Get("next_goal_reward_ready"),
                        NextGoalText.Get("first_session_open_quests"), ScreenEnum.QuestsScreen));
            var daily = QuestManager.GetDailyCommonReward();
            if (daily != null)
                candidates.Add(new NextGoalCandidate(Kind, NextGoalAction.DailyReward, daily.SetId,
                    NextGoalText.Get("next_goal_daily_reward"), null,
                    NextGoalText.Get("first_session_open_quests"), ScreenEnum.QuestsScreen));

            // Активности сохраняют собственные Claim и защиту профиля; карточка открывает их экран.
            if (!ReturnActivityService.CanMutate) return;
            var state = ReturnActivityService.GetSnapshot();
            if (state?.Rewards == null) return;
            foreach (var reward in state.Rewards.Where(item => !item.Claimed).OrderBy(item => item.OriginDay))
                candidates.Add(new NextGoalCandidate(Kind, NextGoalAction.ActivityReward, reward.Id,
                    NextGoalText.Get("next_goal_reward_ready"), NextGoalText.Get(reward.Kind == "week" ? "return_week" : "return_cycle"),
                    NextGoalText.Get("next_goal_open_activities"), ScreenEnum.ReturnActivitiesScreen,
                    activityKind: reward.Kind));
        }
    }
}
