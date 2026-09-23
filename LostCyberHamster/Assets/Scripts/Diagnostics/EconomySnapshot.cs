using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameManagement;
using GameManagement.Progress;
using UnityEngine;

namespace Assets.Scripts.Diagnostics
{
    /// <summary>Содержит только игровые поля для сверки; purchase token и identity аккаунта исключены.</summary>
    [Serializable]
    public sealed class EconomySnapshot
    {
        public int coins, crystals, xp, player_level, development_points;
        public int selected_skin, selected_ability;
        public int[] skins, purchased_skins, abilities;
        public string[] upgrades, campaign, quests, receipts, return_rewards;
        public int return_days;
        public string return_day;
        public bool tutorial_completed;

        public long TotalXp => (long)(player_level - 1) * PlayerExperienceService.PlayerLevelThreshold + xp;

        /// <summary>Копирует экономику без ссылок на изменяемые коллекции сохранения.</summary>
        public static EconomySnapshot Capture(PlayerData p)
        {
            if (p == null) return null;
            return new EconomySnapshot
            {
                coins = p.Money, crystals = p.Crystals, xp = p.ExperiencePoints,
                player_level = p.PlayerLevel, development_points = p.DevelopmentPoints,
                selected_skin = p.AppliedSkinId, selected_ability = p.ActiveSuperAttackId,
                skins = p.UnlockedSkinIds?.OrderBy(x => x).ToArray() ?? Array.Empty<int>(),
                purchased_skins = p.PurchasedSkinIds?.OrderBy(x => x).ToArray() ?? Array.Empty<int>(),
                abilities = p.UnlockedSuperAttackIds?.OrderBy(x => x).ToArray() ?? Array.Empty<int>(),
                upgrades = p.SuperAttackLevels?.Select(x => JsonUtility.ToJson(x)).OrderBy(x => x).ToArray() ?? Array.Empty<string>(),
                campaign = p.Progress.Entries.Select(x => x.Key + ":" + x.Stars).OrderBy(x => x).ToArray(),
                quests = p.QuestStates?.Where(x => x != null).Select(x => x.QuestId + ":" + x.InstanceId + ":" +
                    x.CurrentProgress + ":" + x.IsCompleted + ":" + x.IsRewardClaimed).OrderBy(x => x).ToArray() ?? Array.Empty<string>(),
                receipts = (p.AppliedWeeklyRewardRunIds ?? new()).Select(x => "weekly:" + x)
                    .Concat((p.AppliedRewardedRequestIds ?? new()).Select(x => "rewarded:" + x))
                    .Concat((p.Monetization?.PurchaseTransactionIds ?? new()).Select(x => "purchase:" + Digest(x)))
                    .Concat((p.ReturnActivities?.Rewards ?? new()).Where(x => x.Claimed).Select(x => "activity:" + x.Kind + ":" + x.Id))
                    .OrderBy(x => x).ToArray(),
                return_days = p.ReturnActivities?.TotalDays ?? 0, return_day = p.ReturnActivities?.LastCreditedDay ?? "",
                return_rewards = (p.ReturnActivities?.Rewards ?? new()).Select(x => x.Kind + ":" + x.Id + ":" + x.Claimed).OrderBy(x => x).ToArray(),
                tutorial_completed = p.IsTutorialCompleted
            };
        }

        internal static string Digest(string value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>Одна строка журнала; снимки контрольные, delta применяется только у economy_transaction.</summary>
    [Serializable]
    internal sealed class EconomyEvent
    {
        public int schema_version = 1;
        public string event_id, utc, profile_id, save_generation, session_id, build_version, balance_version;
        public long sequence, revision, lost_packets;
        public string type, source, detail, operation_id, run_id, level, cohort;
        public double active_seconds;
        public int stars, value, previous_best_stars, remaining_lives = -1;
        public bool ads_test_mode, purchases_enabled, interstitial_enabled;
        public bool confirmed;
        public EconomySnapshot before, after;
        public EconomyProgressionState progression_before, progression_after;
        public long xp_delta, coins_delta, crystals_delta, points_delta;
        public EconomyFlow[] flows;
        public EconomyRuntimeState runtime;
    }

    [Serializable]
    internal sealed class EconomyProgressionState
    {
        public string level_address, location_id, part_id, next_level_address, next_part_first_address;
        public int level_index, stars, next_level_stars, location_stars, stars_to_next_location;
        public int current_part_stars, current_part_required_stars;
        public bool level_unlocked, next_level_unlocked, current_part_completed, next_part_first_unlocked;
    }

    [Serializable]
    internal sealed class EconomyFlow
    {
        public string source, resource;
        public long income, expense;
    }

    [Serializable]
    internal sealed class EconomyRuntimeState
    {
        public string action, reason, placement, request_id, loot_disposition;
        public int run_coins, run_crystals, gross_run_coins;
        public int wallet_coins, wallet_crystals;
        public int spend_total, spend_from_run, spend_from_wallet;
        public int lives_after = -1;
        public bool attempt_preserved;
    }
}
