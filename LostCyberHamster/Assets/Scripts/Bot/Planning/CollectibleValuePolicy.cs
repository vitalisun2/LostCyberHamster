using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Считает effective value collectable с учетом текущего projected состояния хомяка.
    /// </summary>
    internal static class CollectibleValuePolicy
    {
        private const int MaxEnergy = 100;
        private const int EnergyCollectibleGain = 30;
        private const int MaxLives = 3;
        private const int LifeCollectibleGain = 1;
        private const int FlatCollectibleValue = 1;

        /// <summary>
        /// Возвращает true, если collectable имеет положительную planning-ценность.
        /// </summary>
        public static bool TryGetPositiveValue(
            HamsterSnapshot hamster,
            ObstacleSnapshot collectible,
            out CollectibleObjectiveValue value)
        {
            value = CollectibleObjectiveValue.None;
            if (collectible == null)
                return false;

            return TryGetPositiveValue(
                hamster,
                collectible.ObstacleType,
                out value);
        }

        /// <summary>
        /// Возвращает true, если тип collectable полезен в текущем projected состоянии.
        /// </summary>
        public static bool TryGetPositiveValue(
            HamsterSnapshot hamster,
            ObstacleTypeEnum obstacleType,
            out CollectibleObjectiveValue value)
        {
            value = CollectibleObjectiveValue.None;
            if (hamster == null || !ObstacleClassifier.IsCollectible(obstacleType))
                return false;

            if (ObstacleClassifier.IsLifeCollectible(obstacleType))
                return TryGetLifeValue(hamster, out value);

            if (ObstacleClassifier.IsEnergyCollectible(obstacleType))
                return TryGetEnergyValue(hamster, out value);

            if (ObstacleClassifier.IsCrystalCollectible(obstacleType))
                return TryGetFlatCollectibleValue(CollectibleKind.Crystal, out value);

            if (ObstacleClassifier.IsCoinCollectible(obstacleType))
                return TryGetFlatCollectibleValue(CollectibleKind.Coin, out value);

            return false;
        }

        /// <summary>
        /// Применяет collectable value к projected snapshot хомяка.
        /// </summary>
        public static HamsterSnapshot ApplyValue(
            HamsterSnapshot hamster,
            CollectibleObjectiveValue value)
        {
            if (hamster == null || !value.HasValue)
                return hamster;

            int energy = hamster.Energy;
            int lives = hamster.Lives;
            if (value.Kind == CollectibleKind.Energy)
                energy = ClampToMax(energy + value.EffectiveGain, MaxEnergy);

            if (value.Kind == CollectibleKind.Life)
                lives = ClampToMax(lives + value.EffectiveGain, MaxLives);

            return new HamsterSnapshot(
                hamster.HamsterState,
                hamster.IsOnBottomLine,
                hamster.IsOnRoof,
                energy,
                lives,
                hamster.IsShifting,
                hamster.RoofSupportInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }

        /// <summary>
        /// Возвращает true, если chain содержит collectable с положительной ценностью для текущего projected состояния.
        /// </summary>
        public static bool HasPositiveCollectible(
            HamsterSnapshot hamster,
            ObstacleChain chain)
        {
            if (hamster == null || chain == null)
                return false;

            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (element == null || !element.HasRole(ObstacleRole.Collectible))
                    continue;

                if (TryGetPositiveValue(
                        hamster,
                        element.Obstacle,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetLifeValue(
            HamsterSnapshot hamster,
            out CollectibleObjectiveValue value)
        {
            int gain = ClampPositiveToMax(MaxLives - hamster.Lives, LifeCollectibleGain);
            value = new CollectibleObjectiveValue(CollectibleKind.Life, gain);
            return value.HasValue;
        }

        private static bool TryGetEnergyValue(
            HamsterSnapshot hamster,
            out CollectibleObjectiveValue value)
        {
            int gain = ClampPositiveToMax(MaxEnergy - hamster.Energy, EnergyCollectibleGain);
            value = new CollectibleObjectiveValue(CollectibleKind.Energy, gain);
            return value.HasValue;
        }

        private static bool TryGetFlatCollectibleValue(
            CollectibleKind collectibleKind,
            out CollectibleObjectiveValue value)
        {
            value = new CollectibleObjectiveValue(collectibleKind, FlatCollectibleValue);
            return true;
        }

        private static int ClampPositiveToMax(int value, int maxValue)
        {
            if (value <= 0)
                return 0;

            return value < maxValue ? value : maxValue;
        }

        private static int ClampToMax(int value, int maxValue)
        {
            return value > maxValue ? maxValue : value;
        }
    }
}
