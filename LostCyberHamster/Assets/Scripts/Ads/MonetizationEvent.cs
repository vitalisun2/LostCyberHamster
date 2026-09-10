using System;
using Unity.Services.Analytics;
using UnityEngine;

namespace GameAds
{
    /// <summary>Событие воронки с placement и сохранённой суммой; выручка берётся из отчётов платформ.</summary>
    public sealed class MonetizationEvent : Unity.Services.Analytics.Event
    {
        private MonetizationEvent(string phase, string placement, string receipt, int amount) : base("monetization")
        {
            SetParameter("mon_schema", 1);
            SetParameter("mon_phase", phase);
            SetParameter("mon_placement", placement);
            // Google TransactionID содержит purchase token: в аналитику передаём только необратимый digest.
            string digest = string.Empty;
            if (!string.IsNullOrEmpty(receipt))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                digest = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(receipt)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            SetParameter("mon_receipt", digest);
            SetParameter("mon_amount", amount);
        }
        public static void Record(string phase, string placement, string receipt = null, int amount = 0)
        {
            Assets.Scripts.Diagnostics.EconomyTelemetry.Record("monetization", phase,
                placement + ":" + Assets.Scripts.Diagnostics.EconomySnapshot.Digest(receipt), amount);
            if (!AnalyticsManager.CanRecordReturnActivity) return;
            try { AnalyticsService.Instance.RecordEvent(new MonetizationEvent(phase, placement, receipt, amount)); }
            catch (Exception exception) { Debug.LogWarning($"[Analytics] Monetization event: {exception.GetType().Name}."); }
        }
    }
}
