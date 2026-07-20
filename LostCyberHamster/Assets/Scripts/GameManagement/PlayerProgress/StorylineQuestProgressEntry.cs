using System;

namespace GameManagement
{
    [Serializable]
    public sealed class StorylineQuestProgressEntry
    {
        public string QuestId;
        public bool IsRewardClaimed;
    }
}
