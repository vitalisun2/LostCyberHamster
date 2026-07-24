using GameManagement;
using NUnit.Framework;
using Vues.GameCore;

namespace Assets.Tests.EditMode
{
    public class BasicQuestLifecycleTests
    {
        private const string QuestId = "quest-002";
        private const int TargetAmount = 5;

        private BasicQuestLifecycle _lifecycle;

        [TearDown]
        public void TearDown()
        {
            _lifecycle?.StopTracking();
        }

        [Test]
        public void Constructor_BindsInitialStateToDefinition()
        {
            var playerData = CreatePlayerData();

            _lifecycle = new BasicQuestLifecycle(CreateDefinition(), playerData);

            Assert.AreEqual(QuestId, playerData.BasicQuest.QuestId);
            Assert.AreEqual(0, playerData.BasicQuest.CurrentProgress);
            Assert.IsFalse(playerData.BasicQuest.IsCompleted);
        }

        [Test]
        public void ObstacleJumpedOver_IncrementsProgressBeforeTarget()
        {
            var playerData = CreatePlayerData();
            StartLifecycle(playerData);

            GameEventsManager.ObstacleJumpedOver("obstacle-1");
            GameEventsManager.ObstacleJumpedOver("obstacle-2");

            Assert.AreEqual(2, playerData.BasicQuest.CurrentProgress);
            Assert.IsFalse(playerData.BasicQuest.IsCompleted);
        }

        [Test]
        public void ObstacleJumpedOver_CompletesAtTarget()
        {
            var playerData = CreatePlayerData();
            StartLifecycle(playerData);

            for (var i = 0; i < TargetAmount; i++)
            {
                GameEventsManager.ObstacleJumpedOver($"obstacle-{i}");
            }

            Assert.AreEqual(TargetAmount, playerData.BasicQuest.CurrentProgress);
            Assert.IsTrue(playerData.BasicQuest.IsCompleted);
        }

        [Test]
        public void ObstacleJumpedOver_CapsProgressAtTarget()
        {
            var playerData = CreatePlayerData();
            playerData.BasicQuest.QuestId = QuestId;
            playerData.BasicQuest.CurrentProgress = TargetAmount - 1;
            StartLifecycle(playerData);

            GameEventsManager.ObstacleJumpedOver("obstacle-1");
            GameEventsManager.ObstacleJumpedOver("obstacle-2");

            Assert.AreEqual(TargetAmount, playerData.BasicQuest.CurrentProgress);
            Assert.IsTrue(playerData.BasicQuest.IsCompleted);
        }

        [Test]
        public void ObstacleJumpedOver_DoesNotChangeCompletedQuest()
        {
            var playerData = CreatePlayerData();
            playerData.BasicQuest.QuestId = QuestId;
            playerData.BasicQuest.CurrentProgress = TargetAmount;
            playerData.BasicQuest.IsCompleted = true;
            StartLifecycle(playerData);

            GameEventsManager.ObstacleJumpedOver("obstacle-1");

            Assert.AreEqual(TargetAmount, playerData.BasicQuest.CurrentProgress);
            Assert.IsTrue(playerData.BasicQuest.IsCompleted);
        }

        [Test]
        public void StopTracking_UnsubscribesFromEvent()
        {
            var playerData = CreatePlayerData();
            StartLifecycle(playerData);

            _lifecycle.StopTracking();
            GameEventsManager.ObstacleJumpedOver("obstacle-1");

            Assert.AreEqual(0, playerData.BasicQuest.CurrentProgress);
            Assert.IsFalse(playerData.BasicQuest.IsCompleted);
        }

        [Test]
        public void StartTracking_DoesNotSubscribeTwice()
        {
            var playerData = CreatePlayerData();
            StartLifecycle(playerData);

            _lifecycle.StartTracking();
            GameEventsManager.ObstacleJumpedOver("obstacle-1");

            Assert.AreEqual(1, playerData.BasicQuest.CurrentProgress);
            Assert.IsFalse(playerData.BasicQuest.IsCompleted);
        }

        private void StartLifecycle(PlayerData playerData)
        {
            _lifecycle = new BasicQuestLifecycle(CreateDefinition(), playerData);
            _lifecycle.StartTracking();
        }

        private static PlayerData CreatePlayerData()
        {
            return new PlayerData();
        }

        private static Quest CreateDefinition()
        {
            return new Quest
            {
                Id = QuestId,
                ActionTypeString = ActionTypeEnum.JumpOverObstacles.ToString(),
                TargetAmount = TargetAmount
            };
        }
    }
}
