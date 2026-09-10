using System;
using GameManagement;

namespace GameAds
{
    /// <summary>Все условия мягкого ограничения применяются совместно.</summary>
    public static class InterstitialPolicy
    {
        public static bool IsEligible(MonetizationState state, DateTime utcNow, int wins, double activeSeconds)
        {
            if (state == null || state.NoAds || !state.InterstitialVariant || state.VisitCount < 2 ||
                wins < MonetizationConfig.MinimumWins || activeSeconds < MonetizationConfig.MinimumActiveSeconds)
                return false;
            var day = utcNow.Date.Ticks;
            return day > state.InterstitialDayUtcTicks || day == state.InterstitialDayUtcTicks &&
                state.InterstitialDayCount < MonetizationConfig.MaximumInterstitialsPerDay;
        }
    }
}
