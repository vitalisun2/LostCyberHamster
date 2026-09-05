using System;
using System.Collections.Generic;
using System.Linq;
using GameManagement;
using UnityEngine;

namespace Assets.Scripts.Account
{
    /// <summary>Сохраняет mapping SDK profiles в том же envelope, что и принятый cloud owner.</summary>
    public static class AccountProfileStore
    {
        private const string Feature = "auth-profiles";
        private const string JournalOwner = "installation";

        public static AccountProfileJournal Read()
        {
            string json = GameDataManager.GetJournalJson(Feature, JournalOwner);
            var journal = string.IsNullOrEmpty(json) ? new AccountProfileJournal() :
                JsonUtility.FromJson<AccountProfileJournal>(json);
            if (journal == null)
                throw new InvalidOperationException("Authentication profile journal is invalid.");
            journal.Bindings ??= new List<AccountProfileBinding>();
            return journal;
        }

        public static string ProfileFor(AccountProfileJournal journal, string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return null;
            return journal.Bindings.FirstOrDefault(entry => entry != null && entry.PlayerId == playerId)?.Profile;
        }

        /// <summary>Возвращает подтверждённую связь конкретного владельца, включая истёкшую сессию.</summary>
        public static bool IsKnownLinkedPlayer(string playerId) => !string.IsNullOrWhiteSpace(playerId) &&
            Read().Bindings.Any(entry => entry != null && entry.PlayerId == playerId && entry.IsLinked);

        public static void RecordVerifiedPlayer(string playerId, string profile, bool confirm, bool isLinked)
        {
            var journal = Read();
            var existing = journal.Bindings.FirstOrDefault(entry => entry != null && entry.PlayerId == playerId);
            if (existing != null && existing.Profile == profile && existing.IsLinked == isLinked &&
                (!confirm || journal.LastConfirmedPlayerId == playerId))
                return;
            Bind(journal, playerId, profile).IsLinked = isLinked;
            if (confirm) journal.LastConfirmedPlayerId = playerId;
            Save(journal);
        }

        public static AccountProfileSwitch BeginSwitch(string originalPlayerId, string originalProfile)
        {
            var journal = Read();
            Bind(journal, originalPlayerId, originalProfile).IsLinked = false;
            journal.LastConfirmedPlayerId = originalPlayerId;
            journal.Pending = new AccountProfileSwitch
            {
                OriginalPlayerId = originalPlayerId,
                OriginalProfile = originalProfile,
                CandidateProfile = "switch_" + Guid.NewGuid().ToString("N").Substring(0, 20)
            };
            Save(journal);
            return journal.Pending;
        }

        public static void RecordCandidate(AccountProfileSwitch operation, string playerId)
        {
            var journal = Read();
            operation.CandidatePlayerId = playerId;
            Bind(journal, playerId, operation.CandidateProfile).IsLinked = true;
            journal.Pending = operation;
            Save(journal);
        }

        public static void CompleteSwitch(string confirmedPlayerId)
        {
            var journal = Read();
            journal.LastConfirmedPlayerId = confirmedPlayerId;
            journal.Pending = null;
            Save(journal);
        }

        private static AccountProfileBinding Bind(AccountProfileJournal journal, string playerId, string profile)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(profile))
                throw new ArgumentException("Verified player and SDK profile are required.");
            var binding = journal.Bindings.FirstOrDefault(entry => entry != null && entry.PlayerId == playerId);
            if (binding == null)
            {
                binding = new AccountProfileBinding { PlayerId = playerId };
                journal.Bindings.Add(binding);
            }
            binding.Profile = profile;
            return binding;
        }

        private static void Save(AccountProfileJournal journal) =>
            GameDataManager.ExecuteTechnicalTransaction(() =>
                GameDataManager.SetJournalJson(Feature, JsonUtility.ToJson(journal), JournalOwner));
    }
}
