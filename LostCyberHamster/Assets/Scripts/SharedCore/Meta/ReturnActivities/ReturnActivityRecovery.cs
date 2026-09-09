using System;
using System.Linq;
using GameManagement;
using UnityEngine;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Сравнивает выбранный профиль с локальным журналом без повторного начисления валюты.</summary>
    public static class ReturnActivityRecovery
    {
        private const string Feature = "return-activities";

        internal static ReturnActivityState ReadJournal()
        {
            string json = GameDataManager.GetJournalJson(Feature);
            if (string.IsNullOrEmpty(json)) return null;
            var state = JsonUtility.FromJson<ReturnActivityState>(json);
            if (!ReturnActivityValidator.IsValid(state))
                throw new InvalidOperationException("Activity journal is invalid.");
            return state;
        }

        internal static void Store(ReturnActivityState state)
        {
            state.Revision = checked(state.Revision + 1);
            GameDataManager.SetJournalJson(Feature, JsonUtility.ToJson(state));
        }

        public static bool IsRequired
        {
            get
            {
                if (!GameDataManager.IsLoaded) return false;
                try
                {
                    var journal = ReadJournal();
                    var data = GameDataManager.PlayerData?.ReturnActivities;
                    if (journal == null) return false;
                    if (data == null || journal.Epoch != data.Epoch || journal.Revision > data.Revision ||
                        journal.TotalDays > data.TotalDays ||
                        string.CompareOrdinal(journal.MaxObservedDay, data.MaxObservedDay) > 0) return true;
                    // Сверяем также свёрнутые Claim и earned: более высокая revision другой ветки их не заменяет.
                    if (journal.ClosedCycleThrough > data.ClosedCycleThrough || journal.ClosedWeekIds.Any(id =>
                        !data.ClosedWeekIds.Contains(id) && !data.Rewards.Any(reward => reward.Id == id && reward.Claimed))) return true;
                    return journal.Rewards.Any(reward =>
                    {
                        var current = data.Rewards.FirstOrDefault(item => item.Id == reward.Id);
                        if (current == null) return !IsClaimed(data, reward);
                        return current.Coins != reward.Coins || current.Gems != reward.Gems ||
                            current.ConfigVersion != reward.ConfigVersion || reward.Claimed && !current.Claimed;
                    });
                }
                catch { return true; }
            }
        }

        private static bool IsClaimed(ReturnActivityState data, ActivityReward reward) =>
            data.Rewards.Any(current => current.Id == reward.Id && current.Claimed) ||
            reward.Kind == "cycle" && reward.Cycle <= data.ClosedCycleThrough ||
            reward.Kind == "week" && data.ClosedWeekIds.Contains(reward.Id);

        /// <summary>По явному действию возвращает локальную историю; баланс выбранного cloud-снимка сохраняется.</summary>
        public static bool RestoreActivityHistory()
        {
            if (!GameDataManager.IsLoaded || !string.IsNullOrEmpty(GameDataManager.ActiveConflictOwner)) return false;
            try
            {
                var journal = ReadJournal();
                if (journal == null) return false;
                GameDataManager.ExecuteTransaction(CheckpointReason.ReturnActivityRecovered, () =>
                {
                    GameDataManager.PlayerData.ReturnActivities = journal;
                    Store(journal);
                }, ReturnActivityService.PublishChanged);
                return true;
            }
            catch { return false; }
        }
    }
}
