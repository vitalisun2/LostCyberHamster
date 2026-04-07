using System.Collections.Generic;
using Assets.Scripts.Bot;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.BotV3
{
    public class BotCommittedPlanTests
    {
        private const float SwitchLaneDecisionTravel = 0.45f * global::Assets.Scripts.Consts.GameSpeedBase;

        [Test]
        public void AdvanceCompletedHead_RemovesCompletedHeadAndPromotesNextReadyStep()
        {
            var first = Step(BotAction.SwitchLane, 1001, 0, 0f);
            var second = Step(BotAction.JumpOver, 1002, 10, 1f);
            var plan = new CurrentPlan();
            plan.ReplaceFrom(
                new BranchCandidate(
                    new List<BranchStep> { first, second },
                    new BranchOutcome(10, true)),
                "test");

            first.MarkCompleted();

            var newHead = plan.AdvanceCompletedHead();

            Assert.AreSame(second, newHead);
            Assert.AreEqual(1, plan.Steps.Count);
            Assert.AreEqual(1002, plan.Head.TargetObstacle.StableId);
        }

        [Test]
        public void IsStrictlyBetterForReplacement_FireTimingOnlyDifference_ReturnsFalse()
        {
            var obstacle = Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 7.04f, 8.44f, 10.0f, 1101);
            var retained = new BranchCandidate(
                new List<BranchStep>
                {
                    new BranchStep(BotAction.SwitchLane, obstacle, 5f, 0f, SwitchLaneDecisionTravel, 0, "retained")
                },
                new BranchOutcome(0, true));
            var candidate = new BranchCandidate(
                new List<BranchStep>
                {
                    new BranchStep(BotAction.SwitchLane, obstacle, 5f, 1f, 1f + SwitchLaneDecisionTravel, 0, "candidate")
                },
                new BranchOutcome(0, true));

            Assert.IsFalse(BranchEvaluator.IsStrictlyBetterForReplacement(candidate, retained));
            Assert.IsFalse(BranchEvaluator.IsStrictlyBetterForReplacement(retained, candidate));
        }

        [Test]
        public void FindBestBranch_NoCurrentProblem_KeepsRetainedTail()
        {
            var classifier = new ObjectClassifier();
            var selector = new BranchSelector();
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 15.0f, 16.4f, 18.0f, 1201)
                });

            var retained = new List<BranchStep>
            {
                Step(BotAction.SwitchLane, 1202, 0, 0f),
                Step(BotAction.JumpOver, 1203, 10, 1f)
            };

            var best = selector.FindBestBranch(snapshot, classifier, retained);

            Assert.IsNotNull(best);
            Assert.AreEqual(2, best.Steps.Count);
            Assert.AreEqual(BotAction.SwitchLane, best.Steps[0].Action);
            Assert.AreEqual(1202, best.Steps[0].TargetObstacle.StableId);
            Assert.AreEqual(1203, best.Steps[1].TargetObstacle.StableId);
        }

        [Test]
        public void TryGetCategory_BigAliveDependsOnHamsterRoofState()
        {
            var classifier = new ObjectClassifier();
            var obstacle = Obs(ObstacleTypeEnum.bigAlive, true, 10.0f, 12.0f, 13.0f, 1301);
            var roadSnapshot = MakeSnapshot(true, new[] { obstacle });
            var roofSnapshot = MakeSnapshot(true, new[] { obstacle });
            roofSnapshot.HamsterOnRoof = true;

            Assert.IsTrue(classifier.TryGetCategory(obstacle, roadSnapshot, out var roadCategory));
            Assert.IsTrue(classifier.TryGetCategory(obstacle, roofSnapshot, out var roofCategory));
            Assert.AreEqual(ObjectCategory.Threat, roadCategory);
            Assert.AreEqual(ObjectCategory.Target, roofCategory);
        }

        private static BranchStep Step(BotAction action, int stableId, int energyCost, float fireWorldShift)
        {
            return new BranchStep(
                action,
                new ObstacleInfo(
                    ObstacleTypeEnum.smallNotAliveRoad,
                    false,
                    10.0f,
                    11.4f,
                    10.7f,
                    13.0f,
                    stableId),
                executeAtDistance: 1.5f,
                fireWorldShift: fireWorldShift,
                completionWorldShift: fireWorldShift + 1f,
                energyCost: energyCost,
                reason: "test");
        }

        private static BotSceneSnapshot MakeSnapshot(bool hamOnBottom, ObstacleInfo[] objects)
        {
            return new BotSceneSnapshot
            {
                HamsterOnBottom = hamOnBottom,
                HamsterOnRoof = false,
                HamsterRightX = -2.96f,
                HamsterWidth = 1.64f,
                Energy = 100,
                Lives = 3,
                VisibleObjects = new List<ObstacleInfo>(objects)
            };
        }

        private static ObstacleInfo Obs(
            ObstacleTypeEnum type,
            bool isTopLane,
            float leftX,
            float rightX,
            float distanceToHamster,
            int stableId)
        {
            return new ObstacleInfo(
                type,
                isTopLane,
                leftX,
                rightX,
                (leftX + rightX) * 0.5f,
                distanceToHamster,
                stableId);
        }
    }
}
