using System;

namespace GameManagement.Progress
{
    /// <summary>Хранит фактическую выплату за повышение до подтверждения окна.</summary>
    [Serializable]
    public sealed class LevelUpReward
    {
        public int PlayerLevel;
        public int DevelopmentPoints;
        public int Coins;
    }
}
