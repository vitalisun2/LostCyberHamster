using System;
using Vues.GameCore;

namespace GameAds
{
    /// <summary>Снимок обещанной награды и владельца показа в локальном журнале.</summary>
    [Serializable]
    public sealed class RewardedAdIntent
    {
        public string RequestId;
        public string ProfileId;
        public string OwnerPlayerId;
        public long Generation;
        public string RunId;
        public int SceneHandle;
        public ResourceType RewardType;
        public int RewardAmount;
        public int ShopItemId;
        public bool IsRevive;
        public bool CompletionReceived;
    }
}