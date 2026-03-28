using System.Collections.Generic;
using Assets.Scripts.BotV3;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.BotV3
{
    public class BotV3PlannerFireShiftTests
    {
        private const float SwitchLaneDecisionTravel = 0.45f * global::Assets.Scripts.Consts.GameSpeedBase;
        private const float SwitchLaneFullTravel = 0.77f * global::Assets.Scripts.Consts.GameSpeedBase;

        private ObjectClassifier _classifier;
        private ProblemResolver _problemResolver;
        private ActionGenerator _actionGenerator;
        private BranchGenerator _branchGenerator;
        private BranchSelector _branchSelector;

        [SetUp]
        public void SetUp()
        {
            _classifier = new ObjectClassifier();
            _problemResolver = new ProblemResolver();
            _actionGenerator = new ActionGenerator();
            _branchGenerator = new BranchGenerator();
            _branchSelector = new BranchSelector();
        }

        [Test]
        public void Generate_DistantSmallRoadWithSoonClearingOtherLane_UsesNearestSafeSwitchLaneShift()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, true, -0.74f, 0.26f, 2.22f, 601),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 13.26f, 14.66f, 16.22f, 602)
                });

            snapshot = _classifier.Classify(snapshot);
            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти safe ветвь.");
            Assert.AreEqual(BotAction.SwitchLane, best.Steps[0].Action,
                "Если целевая линия скоро освободится, planner должен экономить энергию и выбирать SwitchLane.");
            Assert.Greater(best.Steps[0].FireWorldShift, 0f,
                "Если target lane занята в текущем кадре, fire должен сдвигаться к ближайшему safe моменту в будущем.");
        }

        [Test]
        public void Generate_SmallRoadWithBlockedOtherLane_FallsBackToJump()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigNotAlive, true, -0.30f, 3.60f, 2.66f, 701),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 0.71f, 2.11f, 3.67f, 702)
                });

            snapshot = _classifier.Classify(snapshot);
            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти хотя бы ветку с Jump.");
            Assert.AreEqual(BotAction.Jump, best.Steps[0].Action,
                "Если nearest safe момент для SwitchLane не существует до дедлайна, planner должен выбрать Jump.");
        }

        [Test]
        public void Generate_ThreeStepZigZag_BuildsDepthThreeBranch()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 7.04f, 8.44f, 10.0f, 801),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 22.04f, 23.44f, 25.0f, 802),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 37.04f, 38.44f, 40.0f, 803)
                });

            snapshot = _classifier.Classify(snapshot);
            var problem = _problemResolver.ResolveNext(snapshot);
            var firstSteps = _actionGenerator.Generate(snapshot, problem);
            var branches = _branchGenerator.Generate(snapshot, firstSteps, _classifier, _actionGenerator, _problemResolver);

            bool hasDepthThree = branches.Exists(branch => branch.Steps.Count >= 3);
            Assert.IsTrue(hasDepthThree,
                "Planner должен строить lookahead минимум на три решения вперёд.");
        }

        [Test]
        public void ResolveNext_NoSameLaneThreat_ReturnsNull()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 15.0f, 16.4f, 18.0f, 851)
                });

            snapshot = _classifier.Classify(snapshot);
            var problem = _problemResolver.ResolveNext(snapshot);

            Assert.IsNull(problem,
                "Если на текущей линии нет угроз, planner не должен искусственно создавать проблему.");
        }

        [Test]
        public void Generate_TargetsOnlyResolvedProblem()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 15.0f, 16.4f, 18.0f, 861),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 30.0f, 31.4f, 33.0f, 862),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 12.0f, 13.4f, 15.0f, 863)
                });

            snapshot = _classifier.Classify(snapshot);
            var problem = _problemResolver.ResolveNext(snapshot);
            var steps = _actionGenerator.Generate(snapshot, problem);

            Assert.IsNotNull(problem, "Должна быть найдена ближайшая same-lane проблема.");
            Assert.AreEqual(861, problem.SourceObstacle.StableId);
            Assert.IsTrue(steps.TrueForAll(step => step.TargetObstacle.StableId == 861),
                "Все шаги в узле должны генерироваться только для текущей проблемы, а не для произвольных visible objects.");
        }

        [Test]
        public void Generate_SplitTargetLaneBlockingIntervals_ProducesSingleSwitchLaneCandidate()
        {
            var root = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 7.04f, 8.44f, 10.0f, 871),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 22.04f, 23.44f, 25.0f, 872),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 37.04f, 38.44f, 40.0f, 873)
                });

            root = _classifier.Classify(root);

            var switched = new ProjectedWorld().ProjectSnapshot(root, SwitchLaneDecisionTravel);
            switched.HamsterOnBottom = false;
            switched.HamsterOnRoof = false;
            switched = _classifier.Classify(switched);

            var problem = _problemResolver.ResolveNext(switched);
            var steps = _actionGenerator.Generate(switched, problem);

            var switchLaneSteps = steps.FindAll(step =>
                step.Action == BotAction.SwitchLane &&
                step.TargetObstacle.StableId == 872);

            Assert.AreEqual(1, switchLaneSteps.Count,
                "После упрощения planner должен строить только один канонический SwitchLane-кандидат для текущей проблемы.");
        }

        [Test]
        public void Project_ManualImmediateSwitchLaneIntoOccupiedTargetLane_IsUnsafe()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 10.04f, 11.44f, 13.0f, 901),
                    Obs(ObstacleTypeEnum.bigAlive, true, -0.20f, 2.40f, 2.76f, 902)
                });

            snapshot = _classifier.Classify(snapshot);

            var strategy = new SwitchLaneStrategy();
            var projector = new ProjectedWorld();
            var step = new BranchStep(
                BotAction.SwitchLane,
                snapshot.VisibleObjects[0],
                executeAtDistance: 13.0f,
                fireWorldShift: 0f,
                completionWorldShift: SwitchLaneFullTravel,
                energyCost: 0,
                reason: "manual immediate switch");

            var projection = strategy.Project(snapshot, step, projector);

            Assert.IsFalse(projection.IsSafe,
                "Проекция должна отбрасывать немедленный SwitchLane, если target lane занята в swept-окне.");
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
                ObjectCategory.Neutral,
                stableId);
        }
    }
}
