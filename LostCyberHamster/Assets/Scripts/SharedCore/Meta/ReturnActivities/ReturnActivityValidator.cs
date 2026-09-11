using System;
using System.Linq;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Проверяет сохранённую структуру и суммы без изменения уже заработанных наград.</summary>
    public static class ReturnActivityValidator
    {
        public static bool IsValid(ReturnActivityState state)
        {
            if (state == null || state.SchemaVersion != 1 || string.IsNullOrEmpty(state.Epoch) ||
                state.Revision < 0 || state.TotalDays < 0 || state.ClosedCycleThrough < 0 ||
                state.ClosedCycleThrough > state.TotalDays / 7 || state.CycleRewards == null ||
                state.Rewards == null || state.Outbox == null || state.ClosedWeekIds == null ||
                state.Week?.AttemptIds == null || state.Week.Days == null) return false;
            if (state.TotalDays > 0 && (state.CycleRewards.Count != 7 ||
                !ActivityDayPolicy.IsDate(state.LastCreditedDay))) return false;
            if (!string.IsNullOrEmpty(state.MaxObservedDay) && !ActivityDayPolicy.IsDate(state.MaxObservedDay)) return false;
            if (string.CompareOrdinal(state.LastCreditedDay, state.MaxObservedDay) > 0) return false;
            if (state.LastWin != null && (state.LastWin.AttemptId != state.LastAttemptId ||
                state.LastWin.Stars < 1 || state.LastWin.Stars > 3 || string.IsNullOrEmpty(state.LastWin.Level) ||
                !ActivityDayPolicy.IsDate(state.LastWin.Day) ||
                ActivityDayPolicy.WeekOfDay(state.LastWin.Day) != state.LastWin.Week)) return false;

            // Валидируем закреплённые награды и уникальность квитанций.
            if (state.CycleRewards.Count != 0 && state.CycleRewards.Count != 7 ||
                state.CycleRewards.Any(reward => reward == null || reward.Coins < 0 || reward.Gems < 0 ||
                    reward.Coins == 0 && reward.Gems == 0)) return false;
            if (state.Rewards.Any(reward => reward == null || string.IsNullOrEmpty(reward.Id) ||
                reward.Coins < 0 || reward.Gems < 0 || reward.Coins == 0 && reward.Gems == 0 ||
                reward.ConfigVersion < 1 || !ActivityDayPolicy.IsDate(reward.OriginDay) ||
                reward.Kind != "cycle" && reward.Kind != "week" || reward.Presented && !reward.Claimed ||
                reward.Kind == "cycle" && (reward.Step < 1 || reward.Step > 7 || reward.Cycle < 1))) return false;
            if (state.Rewards.Select(reward => reward.Id).Distinct().Count() != state.Rewards.Count) return false;
            foreach (var reward in state.Rewards)
            {
                string expectedId = reward.Kind == "cycle"
                    ? $"{state.Epoch}/cycle/{reward.Cycle}/day/{reward.Step}"
                    : $"{state.Epoch}/week/{ActivityDayPolicy.WeekOfDay(reward.OriginDay)}";
                if (reward.Id != expectedId || reward.Kind == "cycle" &&
                    ((long)reward.Cycle - 1) * 7 + reward.Step > state.TotalDays) return false;
            }

            // Пустая неделя допустима до первого menu checkpoint.
            var week = state.Week;
            if (!string.IsNullOrEmpty(week.Id) && (!ActivityDayPolicy.IsDate(week.Id) ||
                ActivityDayPolicy.WeekOfDay(week.Id) != week.Id || week.ConfigVersion < 1 ||
                week.TargetDays < 1 || week.TargetDays > 7 || week.TargetWins < week.TargetDays || week.Coins <= 0 ||
                week.Days.Any(day => !ActivityDayPolicy.IsDate(day) ||
                    ActivityDayPolicy.WeekOfDay(day) != week.Id) ||
                week.AttemptIds.Any(string.IsNullOrEmpty) ||
                week.AttemptIds.Distinct().Count() != week.AttemptIds.Count ||
                week.Days.Distinct().Count() != week.Days.Count || week.Days.Count > week.AttemptIds.Count ||
                week.Completed && (week.AttemptIds.Count < week.TargetWins || week.Days.Count < week.TargetDays))) return false;
            return state.Outbox.All(item => item != null && !string.IsNullOrEmpty(item.Id));
        }
    }
}
