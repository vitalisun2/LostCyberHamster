using System.Collections.Generic;
using GameManagement;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public sealed class PlayerDataSerializationTests
    {
        [Test]
        public void JsonRoundTrip_PreservesStorylineQuestProgress()
        {
            var source = new PlayerData
            {
                StorylineQuestProgress = new List<StorylineQuestProgressEntry>
                {
                    new StorylineQuestProgressEntry
                    {
                        QuestId = "storyline_quest_01",
                        IsRewardClaimed = true
                    }
                }
            };

            var restored = PlayerData.FromJson(source.ToJson());

            Assert.AreEqual(1, restored.StorylineQuestProgress.Count);
            Assert.AreEqual("storyline_quest_01", restored.StorylineQuestProgress[0].QuestId);
            Assert.IsTrue(restored.StorylineQuestProgress[0].IsRewardClaimed);
        }
    }
}
