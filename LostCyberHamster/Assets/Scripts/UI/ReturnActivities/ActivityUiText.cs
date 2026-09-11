using System;
using System.Globalization;
using Vues.GameCore;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Формирует локализованные подписи из domain-снимков, не создавая прогресс.</summary>
    internal static class ActivityUiText
    {
        public static string Get(string key, params object[] args)
        {
            string text = LocalizationManager.GetLocalizedString("return_" + key);
            return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
        }

        public static string Amount(int coins, int gems) => gems > 0
            ? coins > 0 ? Get("coins_gems", coins, gems) : Get("gems", gems)
            : Get("coins", coins);

        public static string RewardTitle(ActivityRewardSnapshot reward) => reward.Kind == "week"
            ? Get("week_complete") : reward.Step == 7 ? Get("cycle_complete") : Get("day_title", reward.Step);

        public static string NextReset(DateTime now, string dayPolicyVersion = null)
        {
            dayPolicyVersion ??= ActivityDayPolicy.Version;
            var boundary = ActivityDayPolicy.Normalize(now, dayPolicyVersion).Date.AddDays(1);
            if (string.Equals(dayPolicyVersion, ActivityDayPolicy.LegacyVersion, StringComparison.Ordinal))
                boundary = boundary.ToLocalTime();
            return boundary.ToString("dd.MM HH:mm", CultureInfo.CurrentCulture);
        }

        public static string WeekDeadline(DateTime now, string dayPolicyVersion = null)
        {
            dayPolicyVersion ??= ActivityDayPolicy.Version;
            var boundary = ActivityDayPolicy.WeekStart(now, dayPolicyVersion).AddDays(7);
            if (string.Equals(dayPolicyVersion, ActivityDayPolicy.LegacyVersion, StringComparison.Ordinal))
                boundary = boundary.ToLocalTime();
            return boundary.ToString("dd.MM HH:mm", CultureInfo.CurrentCulture);
        }

        public static string Status(bool compact = false)
        {
            if (!ReturnActivityService.IsReady) return Get("loading");
            if (ReturnActivityRecovery.IsRequired) return Get(compact ? "recovery_short" : "recovery_required");
            if (!ReturnActivityService.CanMutate) return Get("profile_pending");
            if (ReturnActivityService.IsClockBlocked) return Get("clock_check");
            if (ReturnActivityConfig.Current?.Enabled != true) return Get("paused");
            return UnityEngine.Application.internetReachability == UnityEngine.NetworkReachability.NotReachable
                ? Get("offline") : Get("local_saved");
        }
    }
}
