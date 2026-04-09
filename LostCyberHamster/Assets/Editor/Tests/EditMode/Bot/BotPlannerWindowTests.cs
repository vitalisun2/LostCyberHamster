using System.Collections.Generic;
using Assets.Scripts.Bot;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.Bot
{
    public class BotPlannerFireShiftTests
    {
        private const float SwitchLaneDecisionTravel = 0.45f * global::Assets.Scripts.Consts.GameSpeedBase;

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
        public void Generate_DistantSmallRoadWithSoonClearingOtherLane_UsesMidPointOfLatestSafeWindow()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, true, -0.74f, 0.26f, 2.22f, 601),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 13.26f, 14.66f, 16.22f, 602)
                });

            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти safe ветвь.");
            Assert.AreEqual(BotAction.SwitchLane, best.Steps[0].Action,
                "Если целевая линия скоро освободится, planner должен экономить энергию и выбирать SwitchLane.");
            Assert.AreEqual(10.54f, best.Steps[0].FireWorldShift, 0.01f,
                "Planner должен выбирать midpoint последнего safe-window, построенного по transition-safe геометрии.");
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

            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти хотя бы ветку с Jump.");
            Assert.AreEqual(BotAction.JumpOver, best.Steps[0].Action,
                "Если nearest safe момент для SwitchLane не существует до дедлайна, planner должен выбрать Jump.");
        }

        [Test]
        public void Generate_BigAliveWhenLandingSafe_OffersSuperJumpCandidate()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, false, 0.50f, 1.50f, 3.46f, 711)
                });

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var steps = _actionGenerator.Generate(snapshot, problem);

            Assert.IsTrue(steps.Exists(step =>
                    step.Action == BotAction.SuperJump &&
                    step.TargetObstacle.StableId == 711),
                "Для bigAlive на дороге planner должен строить SuperJump-кандидат, если projected landing zone свободна.");
        }

        [Test]
        public void Generate_BigAliveWithUnsafeLanding_RejectsSuperJumpCandidate()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, false, 0.50f, 1.50f, 3.46f, 721),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 2.30f, 3.70f, 5.26f, 722)
                });

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var steps = _actionGenerator.Generate(snapshot, problem);

            Assert.IsFalse(steps.Exists(step => step.Action == BotAction.SuperJump),
                "Planner не должен предлагать SuperJump, если projected landing overlap даёт runtime-dangerous столкновение на дороге.");
        }

        [Test]
        public void Generate_BigAliveWithBlockedOtherLane_FallsBackToSuperJump()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, false, 0.50f, 1.50f, 3.46f, 731),
                    Obs(ObstacleTypeEnum.bigNotAlive, true, -0.30f, 3.60f, 2.66f, 732)
                });

            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти safe ветвь для bigAlive, даже когда target lane заблокирована.");
            Assert.AreEqual(BotAction.SuperJump, best.Steps[0].Action,
                "Если SwitchLane недоступен до дедлайна bigAlive, planner должен выбирать SuperJump как road-safe альтернативу.");
            Assert.AreEqual(731, best.Steps[0].TargetObstacle.StableId,
                "SuperJump должен решать исходную угрозу bigAlive, а не объект на соседней полосе.");
        }

        [Test]
        public void Generate_BigAliveThenBottomSmall_PrefersSwitchLaneThenJumpOver()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: false,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, true, 5.49f, 6.49f, 8.45f, 1001),
                    Obs(ObstacleTypeEnum.smallNotAliveRoadAndRoof, false, 5.49f, 6.89f, 8.45f, 1002)
                });

            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти safe ветвь для bigAlive + bottom small.");
            Assert.AreEqual(2, best.Steps.Count,
                "В этом сценарии planner должен выбрать двухшаговую цепочку без ложного zigzag возврата.");
            Assert.AreEqual(BotAction.SwitchLane, best.Steps[0].Action,
                "Первый шаг должен уводить хомяка с линии bigAlive.");
            Assert.AreEqual(1001, best.Steps[0].TargetObstacle.StableId,
                "Первый шаг должен решать исходную угрозу на текущей линии.");
            Assert.AreEqual(BotAction.JumpOver, best.Steps[1].Action,
                "После ухода с bigAlive planner должен перепрыгивать small на целевой линии, а не возвращаться под исходную угрозу.");
            Assert.AreEqual(1002, best.Steps[1].TargetObstacle.StableId,
                "Второй шаг должен решать small obstacle на целевой линии.");
        }

        [Test]
        public void Generate_BigNotAliveWithActiveAvoidanceCommitment_DelaysReturnUntilBigAlivePassed()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, true, -2.44f, -1.44f, 0.52f, 1101),
                    Obs(ObstacleTypeEnum.bigNotAlive, false, 5.46f, 9.34f, 8.42f, 1102)
                });
            snapshot.ActiveAvoidanceCommitments.Add(new AvoidanceCommitment(
                threatStableId: 1101,
                forbiddenLaneBottom: false));

            var best = _branchSelector.FindBestBranch(snapshot, _classifier);

            Assert.IsNotNull(best, "Planner должен найти delayed SwitchLane после release committed lane.");
            Assert.AreEqual(1, best.Steps.Count,
                "После добавления межплановой памяти бот не должен возвращаться под bigAlive и строить лишнюю двухшаговую компенсацию.");
            Assert.AreEqual(BotAction.SwitchLane, best.Steps[0].Action);
            Assert.AreEqual(1102, best.Steps[0].TargetObstacle.StableId);
            Assert.Greater(best.Steps[0].FireWorldShift, 0f,
                "Возврат на committed lane должен откладываться до тех пор, пока avoided bigAlive не будет пройден.");
        }

        [Test]
        public void Project_SwitchLaneSeedsAvoidanceCommitmentIntoProjectedState()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: false,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, true, 5.49f, 6.49f, 8.45f, 1151),
                    Obs(ObstacleTypeEnum.bigNotAlive, false, 9.46f, 13.34f, 12.42f, 1152)
                });

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var steps = _actionGenerator.Generate(snapshot, problem);
            var switchLane = steps.Find(step => step.Action == BotAction.SwitchLane);

            Assert.IsNotNull(switchLane, "Для исходной угрозы должен существовать road SwitchLane-кандидат.");

            var projection = _actionGenerator.Project(snapshot, switchLane);

            Assert.IsTrue(projection.IsSafe, "Проекция первого SwitchLane должна оставаться safe.");
            Assert.IsNotNull(projection.NextState);
            Assert.AreEqual(1, projection.NextState.ActiveAvoidanceCommitments.Count,
                "После проекции SwitchLane planner должен переносить avoidance memory в следующий узел ветки.");
            Assert.AreEqual(1151, projection.NextState.ActiveAvoidanceCommitments[0].ThreatStableId);
        }

        [Test]
        public void ProjectSnapshot_WhenCommittedThreatPassed_PrunesAvoidanceCommitment()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, true, -2.44f, -1.44f, 0.52f, 1201)
                });
            snapshot.ActiveAvoidanceCommitments.Add(new AvoidanceCommitment(
                threatStableId: 1201,
                forbiddenLaneBottom: false));

            var projected = new ProjectedWorld().ProjectSnapshot(snapshot, worldShift: 4.0f);

            Assert.AreEqual(0, projected.ActiveAvoidanceCommitments.Count,
                "Commitment должен сниматься автоматически, когда avoided obstacle ушёл назад.");
        }

        [Test]
        public void Generate_FiveStepZigZag_BuildsDepthFiveBranch()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 7.04f, 8.44f, 10.0f, 801),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 22.04f, 23.44f, 25.0f, 802),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 37.04f, 38.44f, 40.0f, 803),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 52.04f, 53.44f, 55.0f, 804),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 67.04f, 68.44f, 70.0f, 805)
                });

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var firstSteps = _actionGenerator.Generate(snapshot, problem);
            var branches = _branchGenerator.Generate(snapshot, firstSteps, _classifier, _actionGenerator, _problemResolver);

            bool hasDepthFive = branches.Exists(branch => branch.Steps.Count >= 5);
            Assert.IsTrue(hasDepthFive,
                "Planner должен строить lookahead минимум на пять решений вперёд.");
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

            bool hasProblem = _problemResolver.TryResolveNextThreat(snapshot, _classifier, out _);

            Assert.IsFalse(hasProblem,
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

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var steps = _actionGenerator.Generate(snapshot, problem);

            Assert.AreEqual(861, problem.StableId);
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

            var switched = new ProjectedWorld().ProjectSnapshot(root, SwitchLaneDecisionTravel);
            switched.HamsterOnBottom = false;
            switched.HamsterOnRoof = false;

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(switched, _classifier, out var problem));
            var steps = _actionGenerator.Generate(switched, problem);

            var switchLaneSteps = steps.FindAll(step =>
                step.Action == BotAction.SwitchLane &&
                step.TargetObstacle.StableId == 872);

            Assert.AreEqual(1, switchLaneSteps.Count,
                "После упрощения planner должен строить только один канонический SwitchLane-кандидат для текущей проблемы.");
        }

        [Test]
        public void Generate_TargetLaneHasSeparatedSafeWindows_UsesMidPointOfLatestWindow()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 20.04f, 21.44f, 23.0f, 911),
                    Obs(ObstacleTypeEnum.bigAlive, true, -0.20f, 2.40f, 2.76f, 912),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, true, 6.75f, 8.15f, 9.71f, 913)
                });

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var steps = _actionGenerator.Generate(snapshot, problem);

            var switchLane = steps.Find(s => s.Action == BotAction.SwitchLane);
            Assert.IsNotNull(switchLane, "SwitchLane должен быть доступен через последнее непрерывное safe окно.");
            Assert.AreEqual(17.88f, switchLane.FireWorldShift, 0.01f,
                "Когда на target lane есть разорванные safe windows, planner должен брать midpoint последнего окна, а не earliest или global midpoint.");
        }

        [Test]
        public void TryBuildStep_TargetLaneOccupiedImmediately_ShiftsFireMomentBeyondOccupant()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 10.04f, 11.44f, 13.0f, 901),
                    Obs(ObstacleTypeEnum.bigAlive, true, -0.20f, 2.40f, 2.76f, 902)
                });

            Assert.IsTrue(_problemResolver.TryResolveNextThreat(snapshot, _classifier, out var problem));
            var steps = _actionGenerator.Generate(snapshot, problem);

            var switchLane = steps.Find(s => s.Action == BotAction.SwitchLane);
            Assert.IsNotNull(switchLane, "SwitchLane должен быть сгенерирован: после освобождения target lane остаётся достаточно места.");
            Assert.AreEqual(10.0f, switchLane.FireWorldShift, 0.01f,
                "При одном непрерывном safe окне planner должен выбирать его середину, а не earliest shift сразу за препятствием.");
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
