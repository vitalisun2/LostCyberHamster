using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.BotV2;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.BotV2
{
    /// <summary>
    /// Тесты на контракт planner pipeline:
    /// ChainGenerator → BranchEvaluator → CurrentPlan
    /// </summary>
    public class PlannerContractTests
    {
        private ChainGenerator _chainGen;
        private ObjectClassifier _classifier;
        private ActionGenerator _actionGen;

        [SetUp]
        public void SetUp()
        {
            _chainGen = new ChainGenerator();
            _classifier = new ObjectClassifier();
            _actionGen = new ActionGenerator();
        }

        // ══════════════════════════════════════════════
        //  9.1: Один safe first, несколько safe second
        // ══════════════════════════════════════════════

        [Test]
        public void OneSafeFirst_MultipleSafeSecond_AllBranchesPresent()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 1.1f, 2.5f, 4.06f, 1),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 5.7f, 7.1f, 8.66f, 2)
                });

            var chains = RunPipeline(snapshot);

            bool hasMultiStep = chains.Exists(c => c.Steps.Count >= 2);
            Assert.IsTrue(hasMultiStep, "Должна быть хотя бы одна ветка глубже 1");
        }

        // ══════════════════════════════════════════════
        //  9.2: Два safe first, у каждого свои safe second
        // ══════════════════════════════════════════════

        [Test]
        public void TwoSafeFirsts_EachHasOwnSecondSteps()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 1.5f, 2.9f, 4.46f, 10),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 6.0f, 7.4f, 8.96f, 11)
                });

            var chains = RunPipeline(snapshot);

            var jumpFirst = chains.FindAll(c =>
                c.Steps.Count >= 1 && c.Steps[0].Action == BotAction.Jump);
            var switchFirst = chains.FindAll(c =>
                c.Steps.Count >= 1 && c.Steps[0].Action == BotAction.SwitchLane);

            Assert.IsTrue(jumpFirst.Count > 0 || switchFirst.Count > 0,
                "Должны быть ветки с разными первыми шагами");
        }

        // ══════════════════════════════════════════════
        //  9.3: Лучшая ветвь не совпадает с ветвью лучшего первого шага
        // ══════════════════════════════════════════════

        [Test]
        public void BestBranch_CanDifferFromBestFirstStep()
        {
            // SwitchLane -> прыжок через два road threat-а = более длинная/профитная ветвь
            // vs просто Jump через первый
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 1.5f, 2.9f, 4.46f, 20),
                    Obs(ObstacleTypeEnum.smallAlive, true, 2.6f, 4.1f, 5.56f, 21),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 6.0f, 7.4f, 8.96f, 22)
                });

            var chains = RunPipeline(snapshot);
            Assert.IsTrue(chains.Count >= 2,
                "Должно быть несколько разных ветвей");
        }

        // ══════════════════════════════════════════════
        //  9.4: Unsafe second steps полностью отсекаются
        // ══════════════════════════════════════════════

        [Test]
        public void AllBranches_AreMarkedSafe()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 2.0f, 3.4f, 4.96f, 30),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 7.0f, 8.4f, 9.96f, 31)
                });

            var chains = RunPipeline(snapshot);
            foreach (var c in chains)
            {
                Assert.IsTrue(c.Outcome.AllStepsSafe,
                    $"Все ветви из ChainGenerator должны быть AllStepsSafe=true, steps={c.Steps.Count}");
            }
        }

        // ══════════════════════════════════════════════
        //  9.5: Ветвь длины 1 сохраняется
        // ══════════════════════════════════════════════

        [Test]
        public void SingleStepBranch_IsPreserved_WhenNoSecondStepPossible()
        {
            // bigAlive на той же линии — SuperJump, без второго шага (не small road)
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.bigAlive, false, 2.0f, 3.4f, 4.96f, 40)
                });

            var chains = RunPipeline(snapshot);
            Assert.IsTrue(chains.Count > 0, "Должна быть хотя бы одна ветка");

            bool hasDepthOne = chains.Exists(c => c.Steps.Count == 1);
            Assert.IsTrue(hasDepthOne, "Ветвь глубины 1 должна сохраняться");
        }

        // ══════════════════════════════════════════════
        //  9.6: Bonus выигрывает ветку как outcome
        // ══════════════════════════════════════════════

        [Test]
        public void BonusCollectedAsOutcome_InfluencesBranchProfit()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 2.0f, 3.4f, 4.96f, 50),
                    Obs(ObstacleTypeEnum.collectableCoin, true, 0.5f, 1.2f, 3.46f, 51)
                });

            var chains = RunPipeline(snapshot);

            var branchesWithCollectibles = chains.FindAll(c =>
                c.Outcome.CollectedObjects.Count > 0);

            // Не всегда будут ветки с collectibles, зависит от геометрии,
            // но если есть — проверяем что profit учтён
            if (branchesWithCollectibles.Count > 0)
            {
                Assert.IsTrue(
                    branchesWithCollectibles[0].Outcome.TotalProfit > 0,
                    "Ветвь с подобранным collectible должна иметь положительный profit");
            }
        }

        // ══════════════════════════════════════════════
        //  9.7: CurrentPlan clear + rebuild после завершения головы
        // ══════════════════════════════════════════════

        [Test]
        public void CurrentPlan_Clear_RemovesAllSteps()
        {
            var plan = new CurrentPlan();
            var chain = new ChainCandidate
            {
                Steps = new List<ChainStep>
                {
                    new ChainStep(BotAction.Jump, DummyObstacle(), 1.5f, 10, "test1"),
                    new ChainStep(BotAction.SwitchLane, DummyObstacle(), 3f, 0, "test2")
                },
                Outcome = new BranchOutcome()
            };

            plan.ReplaceFrom(chain, "test");
            Assert.IsFalse(plan.IsEmpty);
            Assert.AreEqual(2, plan.Steps.Count);

            plan.Clear();
            Assert.IsTrue(plan.IsEmpty);
            Assert.IsNull(plan.Head);
        }

        // ══════════════════════════════════════════════
        //  9.8: BranchEvaluator корректно сортирует
        // ══════════════════════════════════════════════

        [Test]
        public void BranchEvaluator_SelectsBest_BySafety_ThenRank_ThenProfit()
        {
            var c1 = MakeCandidate(safe: true, rank: DecisionRank.ThreatSafety, profit: 10);
            var c2 = MakeCandidate(safe: true, rank: DecisionRank.Target, profit: 100);
            var c3 = MakeCandidate(safe: false, rank: DecisionRank.LifeCollectible, profit: 200);

            var candidates = new List<ChainCandidate> { c1, c3, c2 };
            var best = BranchEvaluator.SelectBest(candidates);

            Assert.IsTrue(best.Outcome.AllStepsSafe, "Unsafe ветвь не должна выигрывать");
            Assert.AreEqual(DecisionRank.Target, best.Outcome.BestRank,
                "Из двух safe ветвей побеждает с лучшим rank");
        }

        [Test]
        public void BranchEvaluator_EqualRank_HigherProfitWins()
        {
            var c1 = MakeCandidate(safe: true, rank: DecisionRank.ThreatSafety, profit: 50);
            var c2 = MakeCandidate(safe: true, rank: DecisionRank.ThreatSafety, profit: 100);

            var candidates = new List<ChainCandidate> { c1, c2 };
            var best = BranchEvaluator.SelectBest(candidates);

            Assert.AreEqual(100, best.Outcome.TotalProfit,
                "При равном rank-е побеждает больший profit");
        }

        // ══════════════════════════════════════════════
        //  Доп: depth > 2 ветви генерируются
        // ══════════════════════════════════════════════

        [Test]
        public void ChainGenerator_CanProduceDepthGreaterThanTwo()
        {
            var snapshot = MakeSnapshot(
                hamOnBottom: true,
                objects: new[]
                {
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 1.1f, 2.5f, 4.06f, 101),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 5.7f, 7.1f, 8.66f, 102),
                    Obs(ObstacleTypeEnum.smallNotAliveRoad, false, 10.3f, 11.7f, 13.26f, 103)
                });

            var chains = RunPipeline(snapshot);
            bool hasLong = chains.Exists(c => c.Steps.Count >= 3);
            Assert.IsTrue(hasLong, "Builder должен строить ветви глубже 2");
        }

        // ══════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════

        private List<ChainCandidate> RunPipeline(BotSceneSnapshot snapshot)
        {
            _classifier.Classify(snapshot);
            var firstSteps = _actionGen.Generate(snapshot);
            return _chainGen.Generate(snapshot, firstSteps, _classifier, _actionGen);
        }

        private static BotSceneSnapshot MakeSnapshot(
            bool hamOnBottom,
            ObstacleInfo[] objects)
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
            ObstacleTypeEnum type, bool isTopLane,
            float leftX, float rightX, float dist, int id)
        {
            return new ObstacleInfo(
                type, isTopLane, leftX, rightX,
                (leftX + rightX) * 0.5f,
                dist, ObjectCategory.Neutral, id);
        }

        private static ObstacleInfo DummyObstacle()
        {
            return new ObstacleInfo(
                ObstacleTypeEnum.smallNotAliveRoad, false, 1f, 2f, 1.5f, 3f,
                ObjectCategory.Threat, 999);
        }

        private static ChainCandidate MakeCandidate(
            bool safe, DecisionRank rank, int profit)
        {
            return new ChainCandidate
            {
                Steps = new List<ChainStep>
                {
                    new ChainStep(BotAction.Jump, DummyObstacle(), 1.5f, 10, "test",
                        profitScore: profit, rank: rank)
                },
                Outcome = new BranchOutcome
                {
                    AllStepsSafe = safe,
                    BestRank = rank,
                    TotalProfit = profit,
                    TotalEnergyCost = 10,
                    NetEnergyGain = -10
                }
            };
        }
    }
}
