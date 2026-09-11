using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Переносимое состояние прогресса, заработанных наград и исходящих событий.</summary>
    [Serializable]
    public sealed class ReturnActivityState : ISerializationCallbackReceiver
    {
        public int SchemaVersion = 1;
        public string Epoch;
        public string DayPolicyVersion = string.Empty;
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

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>Восстанавливает отсутствие победы из пустого объекта, записанного JsonUtility вместо null.</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (TotalDays == 0 && string.IsNullOrEmpty(LastAttemptId) && LastWin != null &&
                string.IsNullOrEmpty(LastWin.AttemptId) && string.IsNullOrEmpty(LastWin.Level) &&
                LastWin.Stars == 0 && string.IsNullOrEmpty(LastWin.CommittedUtc) &&
                string.IsNullOrEmpty(LastWin.Day) && string.IsNullOrEmpty(LastWin.Week))
            {
                LastWin = null;
            }
        }

        /// <summary>Восстанавливает отсутствующие коллекции старых профилей без ретронаград.</summary>
        public void Normalize()
        {
            Epoch ??= Guid.NewGuid().ToString("N");
            DayPolicyVersion ??= string.Empty;
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
