using System;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Хранит заработанный состав отдельно от выдачи и подтверждения квитанции.</summary>
    [Serializable]
    public sealed class ActivityReward
    {
        public string Id;
        public string Kind;
        public string OriginDay;
        public int Cycle;
        public int Step;
        public int ConfigVersion;
        public int Coins;
        public int Gems;
        public bool Claimed;
        public bool Presented;
    }
}
