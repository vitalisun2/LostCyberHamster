using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    internal enum ObstacleBonusDropKind
    {
        Coins,
        Energy,
        Life,
        Crystals
    }

    internal interface IObstacleBonusDropPolicy
    {
        ObstacleBonusDropKind SelectDrop();
    }

    internal sealed class DefaultObstacleBonusDropPolicy : IObstacleBonusDropPolicy
    {
        public ObstacleBonusDropKind SelectDrop()
        {
            float bonusChance = Random.value;
            if (bonusChance >= 0.3f)
                return ObstacleBonusDropKind.Coins;

            float bonusTypeChance = Random.value;
            if (bonusTypeChance < 0.85f)
                return ObstacleBonusDropKind.Energy;

            if (bonusTypeChance < 0.9f)
                return ObstacleBonusDropKind.Life;

            return ObstacleBonusDropKind.Crystals;
        }
    }

    internal sealed class NoEnergyObstacleBonusDropPolicy : IObstacleBonusDropPolicy
    {
        private readonly IObstacleBonusDropPolicy _basePolicy;

        public NoEnergyObstacleBonusDropPolicy(IObstacleBonusDropPolicy basePolicy)
        {
            _basePolicy = basePolicy;
        }

        public ObstacleBonusDropKind SelectDrop()
        {
            ObstacleBonusDropKind drop = _basePolicy.SelectDrop();
            return drop == ObstacleBonusDropKind.Energy
                ? ObstacleBonusDropKind.Coins
                : drop;
        }
    }

    internal static class ObstacleBonusDropPolicyProvider
    {
        private static readonly IObstacleBonusDropPolicy DefaultPolicy =
            new DefaultObstacleBonusDropPolicy();

        private static readonly IObstacleBonusDropPolicy NoEnergyPolicy =
            new NoEnergyObstacleBonusDropPolicy(DefaultPolicy);

        private static IObstacleBonusDropPolicy _currentPolicy = DefaultPolicy;

        public static IObstacleBonusDropPolicy Current => _currentPolicy ?? DefaultPolicy;

        public static void UseDefault()
        {
            _currentPolicy = DefaultPolicy;
        }

        public static void UseNoEnergyBonuses()
        {
            _currentPolicy = NoEnergyPolicy;
        }
    }
}
