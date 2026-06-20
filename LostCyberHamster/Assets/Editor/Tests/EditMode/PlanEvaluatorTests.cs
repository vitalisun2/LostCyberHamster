using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public sealed class PlanEvaluatorTests
    {
        [Test]
        public void SelectBest_PrefersCheaperEnergyCost_WhenMajorObjectivesAreEqual()
        {
            PlanningBranch jumpOver = CreateBranch(
                energyCost: 10,
                majorObjectives: 1,
                actionKind: BotActionKind.JumpOver);
            PlanningBranch superJumpOver = CreateBranch(
                energyCost: 20,
                majorObjectives: 1,
                actionKind: BotActionKind.SuperJumpOver);

            PlanningBranch best = new PlanEvaluator().SelectBest(new[] { superJumpOver, jumpOver });

            Assert.AreSame(jumpOver, best);
        }

        [Test]
        public void SelectBest_DoesNotSpendExtraEnergyForCoin()
        {
            PlanningBranch noCoin = CreateBranch(energyCost: 0);
            PlanningBranch coinWithCost = CreateBranch(energyCost: 10, coinValue: 1);

            PlanningBranch best = new PlanEvaluator().SelectBest(new[] { coinWithCost, noCoin });

            Assert.AreSame(noCoin, best);
        }

        [Test]
        public void SelectBest_PrefersCoin_WhenEnergyCostIsEqual()
        {
            PlanningBranch noCoin = CreateBranch(energyCost: 0);
            PlanningBranch freeCoin = CreateBranch(energyCost: 0, coinValue: 1);

            PlanningBranch best = new PlanEvaluator().SelectBest(new[] { noCoin, freeCoin });

            Assert.AreSame(freeCoin, best);
        }

        private static PlanningBranch CreateBranch(
            int energyCost,
            int majorObjectives = 0,
            int coinValue = 0,
            BotActionKind actionKind = BotActionKind.PassiveAdvance)
        {
            PlannedAction action = new PlannedAction(
                actionKind,
                triggerX: 0f,
                renderWorldX: 0f,
                completionWorldShift: 0f,
                postFireWorldShift: 0f,
                targetObstacleIndex: 0,
                energyCost: energyCost);
            PlanningBranchMetrics metrics = new PlanningBranchMetrics(
                totalEnergyCost: energyCost,
                tapCount: 0,
                actionCount: 1,
                majorObjectiveCount: majorObjectives,
                lifeCollectibleValue: 0,
                energyCollectibleValue: 0,
                crystalCollectibleValue: 0,
                coinCollectibleValue: coinValue);

            return new PlanningBranch(
                new List<PlannedAction> { action },
                metrics,
                finalNextObstacleIndex: 0,
                finalProjectionWorldShift: 0f);
        }
    }
}
