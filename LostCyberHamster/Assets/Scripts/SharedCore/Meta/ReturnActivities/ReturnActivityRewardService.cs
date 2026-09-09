using System;
using System.Linq;
using GameManagement;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Выдаёт валюту по закреплённому entitlement независимо от презентации и rollover.</summary>
    public static class ReturnActivityRewardService
    {
        public static ActivityRewardSnapshot[] GetRewards() => ReturnActivityService.State?.Rewards
            .Where(reward => !reward.Claimed || !reward.Presented)
            .OrderBy(reward => reward.OriginDay, StringComparer.Ordinal).ThenBy(reward => reward.Id, StringComparer.Ordinal)
            .Select(reward => new ActivityRewardSnapshot(reward)).ToArray() ?? Array.Empty<ActivityRewardSnapshot>();

        public static ActivityClaimResult Claim(ActivityRewardSnapshot expected)
        {
            if (expected == null || !expected.IsCurrent) return ActivityClaimResult.StaleContext;
            if (ReturnActivityRecovery.IsRequired) return ActivityClaimResult.RecoveryRequired;
            if (!ReturnActivityService.CanMutate) return ActivityClaimResult.Unavailable;
            var reward = Find(expected);
            if (reward == null) return ActivityClaimResult.Unavailable;
            if (reward.Claimed) return ActivityClaimResult.AlreadyClaimed;
            try
            {
                GameDataManager.ExecuteTransaction(CheckpointReason.ReturnActivityRewardClaimed, () =>
                {
                    // Начисление каждой валюты и ID входят в одну rollback-границу.
                    reward = Find(expected);
                    if (!expected.IsCurrent || reward == null || reward.Claimed)
                        throw new InvalidOperationException("Activity reward context changed.");
                    if (reward.Coins > 0 && !ResourceManager.AddResource(ResourceType.Coins, reward.Coins, notify: false) ||
                        reward.Gems > 0 && !ResourceManager.AddResource(ResourceType.Crystals, reward.Gems, notify: false))
                        throw new InvalidOperationException("Activity reward cannot be applied.");
                    reward.Claimed = true;
                    ReturnActivityTelemetry.EnqueueReward(ReturnActivityService.State, "claim_succeeded", reward);
                    ReturnActivityRecovery.Store(ReturnActivityService.State);
                }, () =>
                {
                    ResourceManager.NotifyBalancesChangedAfterCommit();
                    if (expected.Coins > 0) GameEventsManager.EarnCoins(expected.Coins);
                    if (expected.Gems > 0) GameEventsManager.EarnCrystals(expected.Gems);
                    ReturnActivityService.PublishChanged();
                });
                return ActivityClaimResult.Claimed;
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[Activities] Claim save failed: {exception.GetType().Name}.");
                return ActivityClaimResult.SaveFailed;
            }
        }

        /// <summary>Подтверждает только показ квитанции; повторное открытие не начисляет валюту.</summary>
        public static bool Acknowledge(ActivityRewardSnapshot expected)
        {
            if (expected == null || !expected.IsCurrent || !ReturnActivityService.CanMutate) return false;
            var reward = Find(expected);
            if (reward?.Claimed != true) return false;
            if (reward.Presented) return true;
            try
            {
                GameDataManager.ExecuteTransaction(CheckpointReason.ReturnActivityRewardPresented, () =>
                {
                    reward.Presented = true;
                    ReturnActivityTelemetry.EnqueueReward(ReturnActivityService.State, "presentation_ack", reward);
                    ReturnActivityService.CompactAcknowledged(ReturnActivityService.State);
                    ReturnActivityRecovery.Store(ReturnActivityService.State);
                }, ReturnActivityService.PublishChanged);
                return true;
            }
            catch { return false; }
        }

        private static ActivityReward Find(ActivityRewardSnapshot expected) => ReturnActivityService.State?.Rewards
            .FirstOrDefault(reward => reward.Id == expected.Id && reward.Coins == expected.Coins &&
                reward.Gems == expected.Gems && reward.ConfigVersion == expected.ConfigVersion);
    }
}
