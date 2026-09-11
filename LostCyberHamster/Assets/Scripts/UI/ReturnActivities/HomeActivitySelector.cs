using System;
using System.Linq;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Выбирает достижимое следующее действие без сравнения процентов разных целей.</summary>
    internal static class HomeActivitySelector
    {
        public static string Cycle(ReturnActivityState state, DateTime utc)
        {
            string dayPolicyVersion = ReturnActivityService.DayPolicyVersion;
            string day = ActivityDayPolicy.Day(utc, dayPolicyVersion);
            var earned = state.Rewards.FirstOrDefault(reward => reward.Kind == "cycle" && !reward.Claimed);
            if (earned != null) return ActivityUiText.Get("home_ready", ActivityUiText.Get("cycle"),
                ActivityUiText.Amount(earned.Coins, earned.Gems));
            if (ReturnActivityService.IsClockBlocked || ReturnActivityConfig.Current?.Enabled != true)
                return ActivityUiText.Get("home_waiting", state.Step);
            if (state.LastCreditedDay == day)
                return ActivityUiText.Get("home_day_done", state.Step, ActivityUiText.NextReset(utc, dayPolicyVersion));
            int nextStep = state.Step == 7 ? 1 : state.Step + 1;
            var amount = state.Step == 7 || state.CycleRewards.Count != 7
                ? ReturnActivityConfig.Current?.Days[nextStep - 1] : state.CycleRewards[nextStep - 1];
            return amount == null ? ActivityUiText.Get("paused") :
                ActivityUiText.Get("home_cycle_action", nextStep, ActivityUiText.Amount(amount.Coins, amount.Gems));
        }

        public static string Week(ReturnActivityState state, DateTime utc)
        {
            string dayPolicyVersion = ReturnActivityService.DayPolicyVersion;
            var earned = state.Rewards.FirstOrDefault(reward => reward.Kind == "week" && !reward.Claimed);
            if (earned != null) return ActivityUiText.Get("home_ready", ActivityUiText.Get("week"),
                ActivityUiText.Amount(earned.Coins, earned.Gems));
            var week = state.Week;
            if (string.IsNullOrEmpty(week.Id)) return ActivityUiText.Get("loading");
            if (week.Completed) return ActivityUiText.Get("home_week_done", ActivityUiText.WeekDeadline(utc, dayPolicyVersion));
            int wins = Math.Max(0, week.TargetWins - week.AttemptIds.Count);
            int days = Math.Max(0, week.TargetDays - week.Days.Count);
            return ActivityUiText.Get("home_week_progress", Math.Min(week.TargetWins, week.AttemptIds.Count),
                week.TargetWins, week.Days.Count, week.TargetDays) +
                (wins == 0 && days > 0 ? " · " + ActivityUiText.Get("more_days", days) : string.Empty);
        }

        public static bool WeekFirst(ReturnActivityState state, DateTime utc)
        {
            var ready = state.Rewards.Where(reward => !reward.Claimed).OrderBy(reward => reward.OriginDay)
                .ThenBy(reward => reward.Id, StringComparer.Ordinal).FirstOrDefault();
            if (ready != null) return ready.Kind == "week";
            var week = state.Week;
            if (week.Completed || string.IsNullOrEmpty(week.Id)) return false;
            int nextDays = week.Days.Count +
                (week.Days.Contains(ActivityDayPolicy.Day(utc, ReturnActivityService.DayPolicyVersion)) ? 0 : 1);
            return week.AttemptIds.Count + 1 >= week.TargetWins && nextDays >= week.TargetDays;
        }
    }
}
