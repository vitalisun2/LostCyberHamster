using System;
using System.Linq;
using Assets.Scripts.Tutorial;
using GameManagement;
using UnityEngine;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Сохраняет прогресс двух активностей внутри штатного результата уровня.</summary>
    public static class ReturnActivityService
    {
        public static event Action Changed;
        internal static ReturnActivityState State => GameDataManager.PlayerData?.ReturnActivities;
        public static DateTime UtcNow
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (GameDataManager.IsProgressionTestingProfile && DevelopmentUtc.HasValue) return DevelopmentUtc.Value;
#endif
                return DateTime.UtcNow;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static DateTime? DevelopmentUtc;
#endif
        public static bool IsReady => GameDataManager.IsLoaded && State != null;
        public static bool IsClockBlocked => IsReady &&
            string.CompareOrdinal(ActivityDayPolicy.Day(UtcNow), State.MaxObservedDay) < 0;
        public static bool CanMutate => IsReady && !TutorialStorage.IsPlayerDataBackupActive &&
            string.IsNullOrEmpty(GameDataManager.ActiveConflictOwner) && !ReturnActivityRecovery.IsRequired;

        /// <summary>Публикует после save; сбой отдельного UI-подписчика не отменяет запись.</summary>
        public static void PublishChanged()
        {
            if (Changed == null) return;
            foreach (Action callback in Changed.GetInvocationList())
                try { callback(); }
                catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>Обновляет календарь при входе/возврате, не начисляя день за посещение.</summary>
        public static void RefreshPeriods()
        {
            var config = ReturnActivityConfig.Current;
            if (!CanMutate || config?.Enabled != true || IsClockBlocked) return;
            var now = UtcNow;
            string day = ActivityDayPolicy.Day(now);
            if (State.MaxObservedDay == day && State.Week.Id == ActivityDayPolicy.Week(now)) return;
            try
            {
                GameDataManager.ExecuteTransaction(CheckpointReason.ReturnActivityPeriodChanged, () =>
                {
                    State.MaxObservedDay = day;
                    WeeklyActivityPolicy.Rotate(State, now, config);
                    ReturnActivityRecovery.Store(State);
                }, PublishChanged);
            }
            catch (Exception exception) { DebugManager.DiagStability($"[Activities] Period save failed: {exception.GetType().Name}."); }
        }

        /// <summary>Вызывается внутри LevelCompleted transaction; вложенного save и UI-событий здесь нет.</summary>
        public static bool ApplyCommittedWin(ActivityAttemptContext attempt, string level, int stars)
        {
            var config = ReturnActivityConfig.Current;
            if (attempt == null || !attempt.IsCurrent || attempt.Level != level || stars < 1 || stars > 3 ||
                !CanMutate || config?.Enabled != true || IsClockBlocked || State.LastAttemptId == attempt.Id) return false;
            var now = UtcNow;
            string day = ActivityDayPolicy.Day(now);

            // Дата и оба прогресса становятся durable вместе с исходным Win.
            State.MaxObservedDay = day;
            WeeklyActivityPolicy.Rotate(State, now, config);
            if (State.Week.AttemptIds.Contains(attempt.Id)) return false;
            State.LastAttemptId = attempt.Id;
            State.LastWin = new ActivityWinReceipt
            {
                AttemptId = attempt.Id, Level = level, Stars = stars,
                CommittedUtc = now.ToString("O"), Day = day, Week = State.Week.Id
            };
            var daily = SevenWinDaysPolicy.Apply(State, day, config);
            bool weeklyWasCompleted = State.Week.Completed;
            var weekly = WeeklyActivityPolicy.Apply(State, attempt.Id, day);
            ReturnActivityTelemetry.Enqueue(State, "eligible_win", "run", day, attempt.Id);
            if (daily != null)
            {
                ReturnActivityTelemetry.Enqueue(State, "day_credited", "cycle", day, daily.Id);
                ReturnActivityTelemetry.EnqueueReward(State, "reward_available", daily);
                if (daily.Step == 7) ReturnActivityTelemetry.EnqueueReward(State, "cycle_completed", daily);
            }
            if (!weeklyWasCompleted)
                ReturnActivityTelemetry.Enqueue(State, "weekly_progress", "week", State.Week.Id, attempt.Id);
            if (weekly != null)
            {
                ReturnActivityTelemetry.EnqueueReward(State, "weekly_completed", weekly);
                ReturnActivityTelemetry.EnqueueReward(State, "reward_available", weekly);
            }
            ReturnActivityRecovery.Store(State);
            return true;
        }

        /// <summary>Возвращает независимый snapshot для представления; UI не получает mutable профиль.</summary>
        public static ReturnActivityState GetSnapshot() => State == null ? null :
            JsonUtility.FromJson<ReturnActivityState>(JsonUtility.ToJson(State));

        internal static void CompactAcknowledged(ReturnActivityState state)
        {
            // Закрытые циклы сворачиваются только при получении и ACK каждого шага.
            int candidate = state.ClosedCycleThrough + 1;
            while (candidate <= state.TotalDays / 7)
            {
                var rewards = state.Rewards.Where(reward => reward.Kind == "cycle" && reward.Cycle == candidate).ToArray();
                if (rewards.Length != 7 || rewards.Any(reward => !reward.Claimed || !reward.Presented)) break;
                state.Rewards.RemoveAll(reward => reward.Kind == "cycle" && reward.Cycle == candidate);
                state.ClosedCycleThrough = candidate++;
            }
            var completedWeeks = state.Rewards.Where(reward => reward.Kind == "week" && reward.Claimed && reward.Presented).ToArray();
            foreach (var reward in completedWeeks)
            {
                if (!state.ClosedWeekIds.Contains(reward.Id)) state.ClosedWeekIds.Add(reward.Id);
                state.Rewards.Remove(reward);
            }
        }
    }
}
