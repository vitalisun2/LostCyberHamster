using System;
using System.Linq;
using GameManagement;
using UnityEngine;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Доставляет события после commit с устойчивым ID; повтор транспорта дедуплицируется в аналитике.</summary>
    public static class ReturnActivityTelemetry
    {
        private static bool _flushing;

        internal static void Enqueue(ReturnActivityState state, string action, string kind, string period, string correlation)
        {
            if (!AnalyticsManager.CanRecordReturnActivity) return;
            state.Outbox.Add(new ReturnActivityEvent
            {
                Id = Guid.NewGuid().ToString("N"), Action = action, Kind = kind, Period = period,
                Correlation = correlation, Step = state.Step, Wins = state.Week.AttemptIds.Count, Days = state.Week.Days.Count
            });
        }

        internal static void EnqueueReward(ReturnActivityState state, string action, ActivityReward reward)
        {
            if (!AnalyticsManager.CanRecordReturnActivity) return;
            state.Outbox.Add(new ReturnActivityEvent
            {
                Id = Guid.NewGuid().ToString("N"), Action = action, Kind = reward.Kind,
                Period = reward.OriginDay, Correlation = reward.Id, Step = reward.Step,
                Coins = reward.Coins, Gems = reward.Gems
            });
        }

        public static void RecordView(string action, string kind)
        {
            if (!AnalyticsManager.CanRecordReturnActivity) return;
            AnalyticsManager.RecordReturnActivity(new ReturnActivityEvent
            {
                Id = Guid.NewGuid().ToString("N"), Action = action, Kind = kind,
                Period = ActivityDayPolicy.Day(DateTime.UtcNow), Correlation = string.Empty
            });
        }

        /// <summary>Отдаёт события штатной очереди SDK и удаляет только переданные ID после сохранения.</summary>
        public static void Flush()
        {
            if (_flushing || !ReturnActivityService.CanMutate || !AnalyticsManager.CanRecordReturnActivity ||
                Application.internetReachability == NetworkReachability.NotReachable) return;
            var pending = ReturnActivityService.State.Outbox.Take(64).ToArray();
            if (pending.Length == 0) return;
            _flushing = true;
            try
            {
                foreach (var item in pending) AnalyticsManager.RecordReturnActivity(item);
                GameDataManager.ExecuteTransaction(CheckpointReason.ReturnActivityTelemetrySent, () =>
                {
                    var ids = pending.Select(item => item.Id).ToHashSet();
                    ReturnActivityService.State.Outbox.RemoveAll(item => ids.Contains(item.Id));
                    ReturnActivityRecovery.Store(ReturnActivityService.State);
                });
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[Activities] Analytics queue failed: {exception.GetType().Name}.");
            }
            finally { _flushing = false; }
        }
    }
}
