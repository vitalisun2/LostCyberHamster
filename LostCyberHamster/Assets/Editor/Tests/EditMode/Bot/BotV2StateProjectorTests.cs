using System.Collections.Generic;
using Assets.Scripts.BotV2;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.BotV2
{
    public class BotV2StateProjectorTests
    {
        private StateProjector _projector;

        [SetUp]
        public void SetUp()
        {
            _projector = new StateProjector();
        }

        [Test]
        public void Project_SwitchLaneToCoin_CollectsCoinAsOutcome()
        {
            var snapshot = new BotSceneSnapshot
            {
                HamsterOnBottom = true,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<ObstacleInfo>
                {
                    MakeObstacle(ObstacleTypeEnum.collectableCoin, true, -1.80f, -1.00f, 1.16f, ObjectCategory.Collectible, 11),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 6.00f, 7.40f, 8.96f, ObjectCategory.Threat, 12)
                }
            };

            var step = new ChainStep(
                BotAction.SwitchLane,
                snapshot.VisibleObjects[0],
                executeAtDistance: 1.16f,
                energyCost: 0,
                reason: "SwitchLane collect coin",
                profitScore: 0,
                rank: DecisionRank.OtherCollectible,
                semantic: StepSemantic.SwitchLane);

            var projection = _projector.Project(snapshot, step);

            Assert.IsTrue(projection.IsSafe);
            Assert.AreEqual(1, projection.CollectedObjects.Count,
                "SwitchLane в целевую линию должен возвращать подобранный coin как outcome");
            Assert.AreEqual(11, projection.CollectedObjects[0].StableId);
            Assert.IsFalse(projection.NextState.HamsterOnBottom,
                "После SwitchLane хомяк должен оказаться на верхней линии");
        }

        [Test]
        public void Project_SwitchLaneAwayFromThreat_StillCollectsSourceLaneCoinBeforeShift()
        {
            var snapshot = new BotSceneSnapshot
            {
                HamsterOnBottom = false,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<ObstacleInfo>
                {
                    MakeObstacle(ObstacleTypeEnum.collectableCoin, true, -1.28f, -0.48f, 1.68f, ObjectCategory.Collectible, 31),
                    MakeObstacle(ObstacleTypeEnum.bigAlive, true, 17.02f, 18.02f, 19.98f, ObjectCategory.Threat, 32)
                }
            };

            var step = new ChainStep(
                BotAction.SwitchLane,
                snapshot.VisibleObjects[1],
                executeAtDistance: 4.0f,
                energyCost: 0,
                reason: "SwitchLane away from threat",
                profitScore: 0,
                rank: DecisionRank.ThreatSafety,
                semantic: StepSemantic.SwitchLane);

            var projection = _projector.Project(snapshot, step);

            Assert.IsTrue(projection.IsSafe);
            Assert.AreEqual(1, projection.CollectedObjects.Count,
                "При длинном добеге до SwitchLane должен учитываться collectible, собранный на исходной линии до манёвра");
            Assert.AreEqual(31, projection.CollectedObjects[0].StableId);
        }

        [Test]
        public void Project_JumpOnSmallAlive_ConsumesTargetAndMovesToBounceLanding()
        {
            var snapshot = new BotSceneSnapshot
            {
                HamsterOnBottom = false,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<ObstacleInfo>
                {
                    MakeObstacle(ObstacleTypeEnum.smallAlive, true, 1.00f, 2.52f, 3.96f, ObjectCategory.Target, 21),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 7.20f, 8.60f, 10.16f, ObjectCategory.Threat, 22)
                }
            };

            var step = new ChainStep(
                BotAction.Jump,
                snapshot.VisibleObjects[0],
                executeAtDistance: 1.5f,
                energyCost: 10,
                reason: "Jump on target smallAlive",
                profitScore: 100,
                rank: DecisionRank.Target,
                semantic: StepSemantic.JumpOnBounce);

            var projection = _projector.Project(snapshot, step);

            Assert.IsTrue(projection.IsSafe);
            Assert.Contains(21, projection.ConsumedObjectIds,
                "Целевой smallAlive должен считаться consumed после JumpOnBounce");
            Assert.AreEqual(92f / 10f, projection.NextState.Energy / 10f, 0.0001f,
                "Энергия после прыжка должна уменьшиться на стоимость действия");
            Assert.AreEqual(2.52f + 3.5f, projection.NextState.HamsterRightX, 0.001f,
                "JumpOnBounce должен приземлять в target.RightX + bounce travel");
        }

        private static ObstacleInfo MakeObstacle(
            ObstacleTypeEnum type,
            bool isTopLane,
            float leftX,
            float rightX,
            float distanceToHamster,
            ObjectCategory category,
            int stableId)
        {
            return new ObstacleInfo(
                type,
                isTopLane,
                leftX,
                rightX,
                (leftX + rightX) * 0.5f,
                distanceToHamster,
                category,
                stableId);
        }
    }
}