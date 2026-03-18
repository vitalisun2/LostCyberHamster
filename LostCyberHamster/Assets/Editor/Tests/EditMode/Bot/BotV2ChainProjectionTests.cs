using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.BotV2
{
    public class BotV2ChainProjectionTests
    {
        private Assets.Scripts.BotV2.ChainGenerator _chainGenerator;
        private Assets.Scripts.BotV2.ObjectClassifier _classifier;
        private Assets.Scripts.BotV2.ActionGenerator _actionGenerator;

        [SetUp]
        public void SetUp()
        {
            _chainGenerator = new Assets.Scripts.BotV2.ChainGenerator();
            _classifier = new Assets.Scripts.BotV2.ObjectClassifier();
            _actionGenerator = new Assets.Scripts.BotV2.ActionGenerator();
        }

        [Test]
        public void Generate_SwitchLaneThenSmallAliveClose_UsesJumpOverForSecondStep()
        {
            var snapshot = new Assets.Scripts.BotV2.BotSceneSnapshot
            {
                HamsterOnBottom = true,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<Assets.Scripts.BotV2.ObstacleInfo>
                {
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 1.70f, 3.10f, 4.66f, 1),
                    MakeObstacle(ObstacleTypeEnum.smallAlive, true, 2.64f, 4.16f, 5.60f, 2),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, true, 9.34f, 10.74f, 12.30f, 3),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, true, 14.94f, 16.34f, 17.90f, 4)
                }
            };

            _classifier.Classify(snapshot);
            var firstSteps = _actionGenerator.Generate(snapshot);
            var chains = _chainGenerator.Generate(snapshot, firstSteps, _classifier, _actionGenerator);

            var matchingChain = chains.Find(chain =>
                chain.Steps.Count >= 2 &&
                chain.Steps[0].Action == Assets.Scripts.BotV2.BotAction.SwitchLane &&
                chain.Steps[0].TargetObstacle.StableId == 1 &&
                chain.Steps[1].TargetObstacle.StableId == 2);

            Assert.IsNotNull(matchingChain,
                "Ожидалась двухшаговая ветка SwitchLane -> Jump по smallAlive");
            Assert.AreEqual(Assets.Scripts.BotV2.StepSemantic.JumpOver, matchingChain.Steps[1].Semantic,
                "После честной проекции post-step состояния smallAlive должна прогнозироваться как JumpOver");

            bool hasWrongSemantic = chains.Exists(chain =>
                chain.Steps.Count >= 2 &&
                chain.Steps[0].Action == Assets.Scripts.BotV2.BotAction.SwitchLane &&
                chain.Steps[0].TargetObstacle.StableId == 1 &&
                chain.Steps[1].TargetObstacle.StableId == 2 &&
                chain.Steps[1].Semantic == Assets.Scripts.BotV2.StepSemantic.JumpOnBounce);

            Assert.IsFalse(hasWrongSemantic,
                "Для этого кейса не должно оставаться ветки SwitchLane -> JumpOnBounce");
        }

        [Test]
        public void Generate_SequentialSmallRoadThreats_BuildsBranchLongerThanTwoSteps()
        {
            var snapshot = new Assets.Scripts.BotV2.BotSceneSnapshot
            {
                HamsterOnBottom = true,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<Assets.Scripts.BotV2.ObstacleInfo>
                {
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 1.10f, 2.50f, 4.06f, 101),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 5.70f, 7.10f, 8.66f, 102),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 10.30f, 11.70f, 13.26f, 103)
                }
            };

            _classifier.Classify(snapshot);
            var firstSteps = _actionGenerator.Generate(snapshot);
            var chains = _chainGenerator.Generate(snapshot, firstSteps, _classifier, _actionGenerator);

            bool hasLongBranch = chains.Exists(chain => chain.Steps.Count >= 3);
            Assert.IsTrue(hasLongBranch,
                "После обобщения builder должен уметь строить ветви глубже двух шагов");
        }

        [Test]
        public void Generate_MultipleSameLaneThreats_SwitchLaneTargetsNearestThreat()
        {
            var snapshot = new Assets.Scripts.BotV2.BotSceneSnapshot
            {
                HamsterOnBottom = false,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<Assets.Scripts.BotV2.ObstacleInfo>
                {
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, true, -1.69f, -0.29f, 1.27f, 201),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, true, 3.91f, 5.31f, 6.87f, 202),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, false, 8.94f, 10.34f, 11.90f, 203),
                    MakeObstacle(ObstacleTypeEnum.smallNotAliveRoad, true, 14.14f, 15.54f, 17.10f, 204)
                }
            };

            _classifier.Classify(snapshot);
            var firstSteps = _actionGenerator.Generate(snapshot);

            var switchLaneSteps = firstSteps.FindAll(step =>
                step.Action == Assets.Scripts.BotV2.BotAction.SwitchLane &&
                step.Rank == Assets.Scripts.BotV2.DecisionRank.ThreatSafety);

            Assert.AreEqual(1, switchLaneSteps.Count,
                "Для ухода от угрозы должен генерироваться один SwitchLane по ближайшей same-lane угрозе");
            Assert.AreEqual(201, switchLaneSteps[0].TargetObstacle.StableId,
                "ThreatSafety SwitchLane должен быть привязан к ближайшей угрозе, чтобы исполниться вовремя");
        }

        private static Assets.Scripts.BotV2.ObstacleInfo MakeObstacle(
            ObstacleTypeEnum type,
            bool isTopLane,
            float leftX,
            float rightX,
            float distanceToHamster,
            int stableId)
        {
            return new Assets.Scripts.BotV2.ObstacleInfo(
                type,
                isTopLane,
                leftX,
                rightX,
                (leftX + rightX) * 0.5f,
                distanceToHamster,
                Assets.Scripts.BotV2.ObjectCategory.Neutral,
                stableId);
        }
    }
}