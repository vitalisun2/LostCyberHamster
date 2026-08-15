using Assets.Scripts.Common.Models;

namespace Vues.GameCore
{
    /// <summary>
    /// Определяет единый gameplay-результат контактов и landing wave Skateboard.
    /// </summary>
    public static class SkateboardInteractionPolicy
    {
        /// <summary>
        /// Фаза взаимодействия, для которой принимается решение.
        /// </summary>
        public enum Phase
        {
            Ride,
            Jump,
            LandingMiss,
            LandingWave
        }

        /// <summary>
        /// Результат, который должен исполнить владелец collision или landing mechanics.
        /// </summary>
        public enum Outcome
        {
            Collect,
            Damage,
            Destroy,
            PreserveSupport,
            BumpOnly,
            Ignore
        }

        /// <summary>
        /// Возвращает результат без изменения obstacle, surface или mode state.
        /// </summary>
        public static Outcome Decide(
            ObstacleTypeEnum obstacleType,
            Phase phase,
            bool startedOnRoof,
            bool isCurrentSupport = false,
            bool isRideSupport = false)
        {
            if (IsCollectable(obstacleType))
            {
                return phase == Phase.LandingWave
                    ? Outcome.BumpOnly
                    : Outcome.Collect;
            }

            if (!IsPhysical(obstacleType))
                return Outcome.Ignore;

            bool isRoof = IsRoof(obstacleType);
            switch (phase)
            {
                case Phase.Ride:
                    return isRoof && isRideSupport
                        ? Outcome.PreserveSupport
                        : Outcome.Damage;
                case Phase.Jump:
                    return startedOnRoof && isRoof
                        ? Outcome.PreserveSupport
                        : Outcome.Destroy;
                case Phase.LandingMiss:
                    return startedOnRoof && isRoof
                        ? Outcome.Destroy
                        : Outcome.Ignore;
                case Phase.LandingWave:
                    if (isRoof && isCurrentSupport)
                        return Outcome.PreserveSupport;
                    if (startedOnRoof && isRoof)
                        return Outcome.BumpOnly;
                    return Outcome.Destroy;
                default:
                    return Outcome.Ignore;
            }
        }

        /// <summary>
        /// Возвращает true для игровых bonus/collectable типов.
        /// </summary>
        public static bool IsCollectable(ObstacleTypeEnum obstacleType)
        {
            return obstacleType is ObstacleTypeEnum.collectableEnergetic
                or ObstacleTypeEnum.collectablePizza
                or ObstacleTypeEnum.collectableCrystal
                or ObstacleTypeEnum.collectableLife
                or ObstacleTypeEnum.collectableCoin;
        }

        /// <summary>
        /// Возвращает true для разрушаемых физических gameplay obstacles.
        /// </summary>
        private static bool IsPhysical(ObstacleTypeEnum obstacleType)
        {
            return obstacleType is ObstacleTypeEnum.smallAlive
                or ObstacleTypeEnum.bigAlive
                or ObstacleTypeEnum.smallNotAliveRoad
                or ObstacleTypeEnum.smallNotAliveRoadAndRoof
                or ObstacleTypeEnum.bigNotAlive
                or ObstacleTypeEnum.mediumNotAlive;
        }

        /// <summary>
        /// Возвращает true для obstacles, которые могут быть roof support.
        /// </summary>
        public static bool IsRoof(ObstacleTypeEnum obstacleType)
        {
            return obstacleType is ObstacleTypeEnum.bigNotAlive
                or ObstacleTypeEnum.mediumNotAlive;
        }
    }
}
