using System;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Считает независимые победы и даты в одной календарной неделе.</summary>
    public static class WeeklyActivityPolicy
    {
        public static bool Rotate(ReturnActivityState state, DateTime now, ReturnActivityConfig config, string dayPolicyVersion)
        {
            string week = ActivityDayPolicy.Week(now, dayPolicyVersion);
            if (string.CompareOrdinal(week, state.Week.Id) <= 0) return false;
            state.Week = new ActivityWeekState
            {
                Id = week, ConfigVersion = config.Version, TargetWins = config.WeeklyWins,
                TargetDays = config.WeeklyDays, Coins = config.WeeklyCoins
            };
            return true;
        }

        public static ActivityReward Apply(ReturnActivityState state, string attempt, string day)
        {
            var week = state.Week;
            if (week.Completed || week.AttemptIds.Contains(attempt)) return null;
            if (week.AttemptIds.Count < week.TargetWins) week.AttemptIds.Add(attempt);
            if (!week.Days.Contains(day)) week.Days.Add(day);
            if (week.AttemptIds.Count < week.TargetWins || week.Days.Count < week.TargetDays) return null;

            // Завершение создаёт entitlement, валюта выдаётся отдельным Claim.
            week.Completed = true;
            var reward = new ActivityReward
            {
                Id = $"{state.Epoch}/week/{week.Id}", Kind = "week", OriginDay = day,
                ConfigVersion = week.ConfigVersion, Coins = week.Coins
            };
            state.Rewards.Add(reward);
            return reward;
        }
    }
}
