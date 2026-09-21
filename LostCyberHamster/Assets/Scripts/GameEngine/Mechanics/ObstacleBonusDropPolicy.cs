using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public enum ObstacleBonusDropKind
    {
        Coins,
        Energy,
        Life,
        Crystals
    }

    public interface IObstacleBonusDropPolicy
    {
        ObstacleBonusDropKind SelectDrop(bool energyFull, bool livesFull);
    }

    /// <summary>
    /// Базовая политика: выбирает вид дропа по весовой таблице абсолютных процентов
    /// (монеты 65, энергия 25, жизнь 5, кристаллы 5). Состояние ресурсов не учитывает.
    /// </summary>
    public sealed class DefaultObstacleBonusDropPolicy : IObstacleBonusDropPolicy
    {
        // Порядок массивов должен совпадать: DropKinds[i] выпадает с весом DropWeights[i].
        private static readonly ObstacleBonusDropKind[] DropKinds =
        {
            ObstacleBonusDropKind.Coins,
            ObstacleBonusDropKind.Energy,
            ObstacleBonusDropKind.Life,
            ObstacleBonusDropKind.Crystals,
        };

        private static readonly float[] DropWeights = { 65f, 25f, 5f, 5f };

        public ObstacleBonusDropKind SelectDrop(bool energyFull, bool livesFull)
        {
            // Базовые веса не зависят от состояния ресурсов.
            return ResolveByWeights(Random.value * TotalWeight());
        }

        /// <summary>Выбирает вариант по значению броска в диапазоне [0, сумма весов).</summary>
        public static ObstacleBonusDropKind ResolveByWeights(float roll)
        {
            for (int i = 0; i < DropKinds.Length; ++i)
            {
                roll -= DropWeights[i];
                if (roll < 0f)
                    return DropKinds[i];
            }

            return DropKinds[DropKinds.Length - 1];
        }

        private static float TotalWeight()
        {
            float total = 0f;
            foreach (float weight in DropWeights)
                total += weight;
            return total;
        }
    }

    /// <summary>
    /// Игровое правило полноты ресурсов: при полной энергии энергия не выпадает,
    /// при полных жизнях жизнь не выпадает. Исключённая доля автоматически
    /// перераспределяется остальным вариантам: повторный бросок даёт условное
    /// распределение базовой политики по разрешённым вариантам.
    /// </summary>
    public sealed class StateAwareObstacleBonusDropPolicy : IObstacleBonusDropPolicy
    {
        private const int MaxRollAttempts = 8;

        private readonly IObstacleBonusDropPolicy _basePolicy;

        public StateAwareObstacleBonusDropPolicy(IObstacleBonusDropPolicy basePolicy)
        {
            _basePolicy = basePolicy;
        }

        public ObstacleBonusDropKind SelectDrop(bool energyFull, bool livesFull)
        {
            for (int attempt = 0; attempt < MaxRollAttempts; ++attempt)
            {
                ObstacleBonusDropKind drop = _basePolicy.SelectDrop(energyFull, livesFull);
                if (!IsExcluded(drop, energyFull, livesFull))
                    return drop;
            }

            // Монеты никогда не исключаются — безопасный fallback после серии исключённых бросков.
            return ObstacleBonusDropKind.Coins;
        }

        private static bool IsExcluded(ObstacleBonusDropKind drop, bool energyFull, bool livesFull)
        {
            return (energyFull && drop == ObstacleBonusDropKind.Energy)
                || (livesFull && drop == ObstacleBonusDropKind.Life);
        }
    }

    /// <summary>Ботовая политика: энергия не выпадает вовсе, а подменяется монетами.</summary>
    public sealed class NoEnergyObstacleBonusDropPolicy : IObstacleBonusDropPolicy
    {
        private readonly IObstacleBonusDropPolicy _basePolicy;

        public NoEnergyObstacleBonusDropPolicy(IObstacleBonusDropPolicy basePolicy)
        {
            _basePolicy = basePolicy;
        }

        public ObstacleBonusDropKind SelectDrop(bool energyFull, bool livesFull)
        {
            ObstacleBonusDropKind drop = _basePolicy.SelectDrop(energyFull, livesFull);
            return drop == ObstacleBonusDropKind.Energy
                ? ObstacleBonusDropKind.Coins
                : drop;
        }
    }

    public static class ObstacleBonusDropPolicyProvider
    {
        private static readonly IObstacleBonusDropPolicy DefaultPolicy =
            new StateAwareObstacleBonusDropPolicy(new DefaultObstacleBonusDropPolicy());

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