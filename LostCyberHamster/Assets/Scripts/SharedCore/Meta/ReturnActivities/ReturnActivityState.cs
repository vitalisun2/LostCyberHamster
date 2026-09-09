using System;
using System.Collections.Generic;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Переносимое состояние прогресса, заработанных наград и исходящих событий.</summary>
    [Serializable]
    public sealed class ReturnActivityState
    {
        public int SchemaVersion = 1;
        public string Epoch;
        public long Revision;
        public int TotalDays;
        public string LastCreditedDay;
        public string MaxObservedDay;
        public int CycleConfigVersion;
        public List<ActivityCurrencyReward> CycleRewards = new();
        public ActivityWeekState Week = new();
        public List<ActivityReward> Rewards = new();
        public List<ReturnActivityEvent> Outbox = new();
        public string LastAttemptId;
        public ActivityWinReceipt LastWin;
        public int ClosedCycleThrough;
        public List<string> ClosedWeekIds = new();

        public int Cycle => TotalDays == 0 ? 1 : (TotalDays - 1) / 7 + 1;
        public int Step => TotalDays == 0 ? 0 : (TotalDays - 1) % 7 + 1;

        /// <summary>Восстанавливает отсутствующие коллекции старых профилей без ретронаград.</summary>
        public void Normalize()
        {
            Epoch ??= Guid.NewGuid().ToString("N");
            CycleRewards ??= new();
            Week ??= new();
            Week.AttemptIds ??= new();
            Week.Days ??= new();
            Rewards ??= new();
            Outbox ??= new();
            ClosedWeekIds ??= new();
        }
    }
}
