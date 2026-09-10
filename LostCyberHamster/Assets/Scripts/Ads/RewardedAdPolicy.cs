using System;
using GameManagement;

namespace GameAds
{
    /// <summary>Интервал магазинной награды по реальному UTC-времени, включая время вне игры.</summary>
    public static class RewardedAdPolicy
    {
        public static readonly TimeSpan ShopCooldown = TimeSpan.FromMinutes(10);

        /// <summary>Старый профиль без отметки доступен сразу; откат часов сохраняет ожидание.</summary>
        public static TimeSpan ShopCooldownRemaining(MonetizationState state, DateTime utcNow)
        {
            long claimed = state?.LastShopRewardUtcTicks ?? 0;
            if (claimed == 0)
                return TimeSpan.Zero;
            if (claimed < 0 || claimed > DateTime.MaxValue.Ticks)
                throw new ArgumentOutOfRangeException(nameof(state));
            if (utcNow.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Shop cooldown requires UTC time.", nameof(utcNow));

            // Ограничиваем сложение краем DateTime и не сокращаем паузу при переводе часов назад.
            long availableAt = claimed + Math.Min(ShopCooldown.Ticks, DateTime.MaxValue.Ticks - claimed);
            return TimeSpan.FromTicks(Math.Max(0, availableAt - utcNow.Ticks));
        }
    }
}
