using System;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Состав валютной награды, закрепляемый при создании цикла.</summary>
    [Serializable]
    public sealed class ActivityCurrencyReward
    {
        public int Coins;
        public int Gems;
        public ActivityCurrencyReward Copy() => new() { Coins = Coins, Gems = Gems };
    }
}
