using System.Collections.Generic;
using GameManagement;
using NUnit.Framework;
using Vues.GameCore.Quests;

namespace Assets.Tests.EditMode
{
    public sealed class PlayerDataSerializationTests
    {
        [Test]
        public void JsonRoundTrip_PreservesQuestStates()
        {
            var source = new PlayerData
            {
                QuestStates = new List<Quest>
                {
                    new Quest
                    {
                        QuestId = "storyline_quest_01",
                        CurrentProgress = 5,
                        IsCompleted = true,
                        IsRewardClaimed = true,
                        CountedLevelKeys = new List<string>
                        {
                            "01_New_York:Morning:0",
                            "01_New_York:Morning:1"
                        }
                    }
                }
            };

            var restored = PlayerData.FromJson(source.ToJson());

            Assert.AreEqual(1, restored.QuestStates.Count);
            Assert.AreEqual(
                "storyline_quest_01",
                restored.QuestStates[0].QuestId);
            Assert.AreEqual(5, restored.QuestStates[0].CurrentProgress);
            Assert.IsTrue(restored.QuestStates[0].IsCompleted);
            Assert.IsTrue(restored.QuestStates[0].IsRewardClaimed);
            CollectionAssert.AreEqual(
                source.QuestStates[0].CountedLevelKeys,
                restored.QuestStates[0].CountedLevelKeys);
        }
    }
}
