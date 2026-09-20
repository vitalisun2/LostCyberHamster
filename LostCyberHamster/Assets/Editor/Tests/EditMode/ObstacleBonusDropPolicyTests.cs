using Assets.Scripts.GameEngine.Mechanics;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Tests.EditMode
{
    public sealed class ObstacleBonusDropPolicyTests
    {
        private const int SampleCount = 200_000;
        private const float Tolerance = 0.005f; // ±0.5 п.п.

        [Test]
        public void ResolveByWeights_Boundaries_FollowWeightTable()
        {
            Assert.AreEqual(ObstacleBonusDropKind.Coins, DefaultObstacleBonusDropPolicy.ResolveByWeights(0f));
            Assert.AreEqual(ObstacleBonusDropKind.Coins, DefaultObstacleBonusDropPolicy.ResolveByWeights(64.99f));
            Assert.AreEqual(ObstacleBonusDropKind.Energy, DefaultObstacleBonusDropPolicy.ResolveByWeights(65f));
            Assert.AreEqual(ObstacleBonusDropKind.Energy, DefaultObstacleBonusDropPolicy.ResolveByWeights(89.99f));
            Assert.AreEqual(ObstacleBonusDropKind.Life, DefaultObstacleBonusDropPolicy.ResolveByWeights(90f));
            Assert.AreEqual(ObstacleBonusDropKind.Life, DefaultObstacleBonusDropPolicy.ResolveByWeights(94.99f));
            Assert.AreEqual(ObstacleBonusDropKind.Crystals, DefaultObstacleBonusDropPolicy.ResolveByWeights(95f));
            Assert.AreEqual(ObstacleBonusDropKind.Crystals, DefaultObstacleBonusDropPolicy.ResolveByWeights(99.99f));
            Assert.AreEqual(ObstacleBonusDropKind.Crystals, DefaultObstacleBonusDropPolicy.ResolveByWeights(100f));
        }

        [Test]
        public void DefaultPolicy_Distribution_MatchesWeightTable()
        {
            Random.InitState(123456789);
            int[] counts = Sweep(new DefaultObstacleBonusDropPolicy(), false, false);

            AssertShare(counts, ObstacleBonusDropKind.Coins, 0.65f);
            AssertShare(counts, ObstacleBonusDropKind.Energy, 0.25f);
            AssertShare(counts, ObstacleBonusDropKind.Life, 0.05f);
            AssertShare(counts, ObstacleBonusDropKind.Crystals, 0.05f);
        }

        [Test]
        public void StateAware_EnergyFull_RedistributesEnergyShare()
        {
            Random.InitState(987654321);
            var policy = new StateAwareObstacleBonusDropPolicy(new DefaultObstacleBonusDropPolicy());
            int[] counts = Sweep(policy, energyFull: true, livesFull: false);

            AssertShare(counts, ObstacleBonusDropKind.Coins, 65f / 75f);
            AssertShare(counts, ObstacleBonusDropKind.Life, 5f / 75f);
            AssertShare(counts, ObstacleBonusDropKind.Crystals, 5f / 75f);
            AssertShare(counts, ObstacleBonusDropKind.Energy, 0f);
        }

        [Test]
        public void StateAware_LivesFull_RedistributesLivesShare()
        {
            Random.InitState(123987456);
            var policy = new StateAwareObstacleBonusDropPolicy(new DefaultObstacleBonusDropPolicy());
            int[] counts = Sweep(policy, energyFull: false, livesFull: true);

            AssertShare(counts, ObstacleBonusDropKind.Coins, 65f / 95f);
            AssertShare(counts, ObstacleBonusDropKind.Energy, 25f / 95f);
            AssertShare(counts, ObstacleBonusDropKind.Crystals, 5f / 95f);
            AssertShare(counts, ObstacleBonusDropKind.Life, 0f);
        }

        [Test]
        public void StateAware_BothFull_KeepsOnlyCoinsAndCrystals()
        {
            Random.InitState(456789123);
            var policy = new StateAwareObstacleBonusDropPolicy(new DefaultObstacleBonusDropPolicy());
            int[] counts = Sweep(policy, energyFull: true, livesFull: true);

            AssertShare(counts, ObstacleBonusDropKind.Coins, 65f / 70f);
            AssertShare(counts, ObstacleBonusDropKind.Crystals, 5f / 70f);
            AssertShare(counts, ObstacleBonusDropKind.Energy, 0f);
            AssertShare(counts, ObstacleBonusDropKind.Life, 0f);
        }

        [Test]
        public void StateAware_EnergyFull_SkipsEnergyDrop()
        {
            var basePolicy = new ScriptedDropPolicy(
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Life);
            var policy = new StateAwareObstacleBonusDropPolicy(basePolicy);

            Assert.AreEqual(ObstacleBonusDropKind.Life, policy.SelectDrop(energyFull: true, livesFull: false));
        }

        [Test]
        public void StateAware_LivesFull_SkipsLifeDrop()
        {
            var basePolicy = new ScriptedDropPolicy(
                ObstacleBonusDropKind.Life, ObstacleBonusDropKind.Energy);
            var policy = new StateAwareObstacleBonusDropPolicy(basePolicy);

            Assert.AreEqual(ObstacleBonusDropKind.Energy, policy.SelectDrop(energyFull: false, livesFull: true));
        }

        [Test]
        public void StateAware_NotFull_PassesThrough()
        {
            var basePolicy = new ScriptedDropPolicy(
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Life);
            var policy = new StateAwareObstacleBonusDropPolicy(basePolicy);

            Assert.AreEqual(ObstacleBonusDropKind.Energy, policy.SelectDrop(false, false));
            Assert.AreEqual(ObstacleBonusDropKind.Life, policy.SelectDrop(false, false));
        }

        [Test]
        public void StateAware_ExhaustedRerolls_FallsBackToCoins()
        {
            var allEnergy = new[]
            {
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Energy,
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Energy,
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Energy,
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Energy,
            };
            var policy = new StateAwareObstacleBonusDropPolicy(new ScriptedDropPolicy(allEnergy));

            Assert.AreEqual(ObstacleBonusDropKind.Coins, policy.SelectDrop(energyFull: true, livesFull: false));
        }

        [Test]
        public void NoEnergyPolicy_MapsEnergyToCoinsAndForwardsOtherDrops()
        {
            var basePolicy = new ScriptedDropPolicy(
                ObstacleBonusDropKind.Energy, ObstacleBonusDropKind.Life);
            var policy = new NoEnergyObstacleBonusDropPolicy(basePolicy);

            Assert.AreEqual(ObstacleBonusDropKind.Coins, policy.SelectDrop(false, false));
            Assert.AreEqual(ObstacleBonusDropKind.Life, policy.SelectDrop(false, false));
        }

        [Test]
        public void Provider_DefaultIsStateAware()
        {
            ObstacleBonusDropPolicyProvider.UseDefault();

            Assert.IsInstanceOf<StateAwareObstacleBonusDropPolicy>(ObstacleBonusDropPolicyProvider.Current);
        }

        [Test]
        public void Provider_NoEnergyMode_SwitchesPolicy()
        {
            ObstacleBonusDropPolicyProvider.UseNoEnergyBonuses();

            Assert.IsInstanceOf<NoEnergyObstacleBonusDropPolicy>(ObstacleBonusDropPolicyProvider.Current);

            ObstacleBonusDropPolicyProvider.UseDefault();
        }

        private static int[] Sweep(IObstacleBonusDropPolicy policy, bool energyFull, bool livesFull)
        {
            int[] counts = new int[4];
            for (int i = 0; i < SampleCount; ++i)
            {
                ObstacleBonusDropKind drop = policy.SelectDrop(energyFull, livesFull);
                ++counts[KindIndex(drop)];
            }

            return counts;
        }

        private static void AssertShare(int[] counts, ObstacleBonusDropKind kind, float expected)
        {
            float actual = (float) counts[KindIndex(kind)] / SampleCount;
            Assert.AreEqual(expected, actual, Tolerance);
        }

        private static int KindIndex(ObstacleBonusDropKind kind)
        {
            switch (kind)
            {
                case ObstacleBonusDropKind.Coins:
                    return 0;
                case ObstacleBonusDropKind.Energy:
                    return 1;
                case ObstacleBonusDropKind.Life:
                    return 2;
                default:
                    return 3;
            }
        }

        private sealed class ScriptedDropPolicy : IObstacleBonusDropPolicy
        {
            private readonly ObstacleBonusDropKind[] _sequence;
            private int _index;

            public ScriptedDropPolicy(params ObstacleBonusDropKind[] sequence)
            {
                _sequence = sequence;
            }

            public ObstacleBonusDropKind SelectDrop(bool energyFull, bool livesFull)
            {
                int index = _index < _sequence.Length ? _index : _sequence.Length - 1;
                ++_index;
                return _sequence[index];
            }
        }
    }
}