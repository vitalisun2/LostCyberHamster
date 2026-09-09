using GameManagement;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Закрепляет предъявленный приз и контекст профиля до нажатия Claim.</summary>
    public sealed class ActivityRewardSnapshot
    {
        public string ProfileId { get; }
        public long Generation { get; }
        public string Owner { get; }
        public string Id { get; }
        public string Kind { get; }
        public string OriginDay { get; }
        public int Cycle { get; }
        public int Step { get; }
        public int Coins { get; }
        public int Gems { get; }
        public int ConfigVersion { get; }
        public bool Claimed { get; }
        public bool Presented { get; }

        public ActivityRewardSnapshot(ActivityReward reward)
        {
            ProfileId = GameDataManager.ProfileId;
            Generation = GameDataManager.Generation;
            Owner = GameDataManager.OwnerPlayerId;
            Id = reward.Id; Kind = reward.Kind; OriginDay = reward.OriginDay;
            Cycle = reward.Cycle; Step = reward.Step; Coins = reward.Coins; Gems = reward.Gems;
            ConfigVersion = reward.ConfigVersion; Claimed = reward.Claimed; Presented = reward.Presented;
        }

        public bool IsCurrent => ProfileId == GameDataManager.ProfileId &&
            Generation == GameDataManager.Generation && Owner == GameDataManager.OwnerPlayerId;
    }
}
