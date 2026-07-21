using System.Collections.Generic;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.Progress;
using NUnit.Framework;
using Vues.GameCore;

namespace Assets.Tests.EditMode
{
    public sealed class CloudSaveSnapshotCodecTests
    {
        [Test]
        public void CaptureRoundTrip_PreservesFullPlayerDataAndMetadata()
        {
            var source = CreatePlayerData();

            var captured = CloudSaveSnapshotCodec.Capture(
                source,
                "player-01",
                "revision-02",
                "revision-01");
            var serialized = CloudSaveSnapshotCodec.Serialize(captured);
            var restoredSnapshot = CloudSaveSnapshotCodec.Deserialize(serialized);
            var restoredPlayerData = CloudSaveSnapshotCodec.RestorePlayerData(restoredSnapshot);

            Assert.AreEqual(source.ToJson(), restoredPlayerData.ToJson());
            Assert.AreEqual("player-01", restoredSnapshot.PlayerId);
            Assert.AreEqual("revision-02", restoredSnapshot.Revision);
            Assert.AreEqual("revision-01", restoredSnapshot.BaseRevision);
            Assert.AreEqual(source.LastSaveDate, restoredSnapshot.SavedAtUtc);
        }

        [Test]
        public void Capture_SourceChangedLater_SnapshotRemainsUnchanged()
        {
            var source = CreatePlayerData();
            var captured = CloudSaveSnapshotCodec.Capture(source, "player-01");

            source.Money = 999;
            source.PurchasedSkinIds.Add(3);
            source.DailyTasks[0].CurrentAmount = 9;
            source.StorylineQuestProgress[0].IsRewardClaimed = false;
            source.Progress = new LevelProgressSnapshot(new[]
            {
                new LevelProgressEntry(new LevelProgressKey("location_02", "Night", 4), true, 1)
            });

            var restored = CloudSaveSnapshotCodec.RestorePlayerData(captured);

            Assert.AreEqual(125, restored.Money);
            CollectionAssert.AreEqual(new[] { 0, 2 }, restored.PurchasedSkinIds);
            Assert.AreEqual(2, restored.DailyTasks[0].CurrentAmount);
            Assert.IsTrue(restored.StorylineQuestProgress[0].IsRewardClaimed);
            Assert.IsTrue(restored.Progress.TryGet(
                new LevelProgressKey("location_01", "Day", 2),
                out var progress));
            Assert.AreEqual(3, progress.Stars);
        }

        private static PlayerData CreatePlayerData()
        {
            return new PlayerData
            {
                Money = 125,
                Crystals = 7,
                AppliedSkinId = 2,
                PurchasedSkinIds = new List<int> { 0, 2 },
                CurrentLevel = "level_03",
                DailyTasksRefreshDate = "2026-07-21",
                DailyTasks = new List<Quest>
                {
                    new Quest
                    {
                        Id = "daily-01",
                        Title = "Collect",
                        Description = "Collect items",
                        TargetAmount = 5,
                        CurrentAmount = 2,
                        RewardTypeId = 1,
                        RewardAmount = 10,
                        ActionTypeString = "Collect"
                    }
                },
                Progress = new LevelProgressSnapshot(new[]
                {
                    new LevelProgressEntry(new LevelProgressKey("location_01", "Day", 2), true, 3)
                }),
                StorylineQuestProgress = new List<StorylineQuestProgressEntry>
                {
                    new StorylineQuestProgressEntry
                    {
                        QuestId = "story-01",
                        IsRewardClaimed = true
                    }
                },
                LastSaveDate = "2026-07-21T10:15:30.0000000Z",
                IsFirstLaunch = false,
                IsTutorialCompleted = true,
                IsAccountPromptPending = true,
                IsAccountPromptShown = false
            };
        }
    }
}
