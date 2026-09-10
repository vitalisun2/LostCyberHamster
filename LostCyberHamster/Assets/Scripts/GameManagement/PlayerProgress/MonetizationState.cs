using System;

namespace GameManagement
{
    /// <summary>Сохранённые ограничения монетизации текущего владельца профиля.</summary>
    [Serializable]
    public sealed class MonetizationState
    {
        public long LastShopRewardUtcTicks;
    }
}
