using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Vues.GameCore
{
    /// <summary>
    /// Определяет единый gameplay-результат контактов и landing wave Skateboard.
    /// </summary>
    public static class SkateboardInteractionPolicy
    {
        /// <summary>
        /// Результат, который должен исполнить владелец collision или landing runtime.
        /// </summary>
        public enum Outcome
        {
            Collect,
            Destroy,
            PreserveSupport,
            BumpOnly,
            Ignore
        }

        /// <summary>
        /// Возвращает результат контакта во время езды.
        /// </summary>
        public static Outcome DecideRide(
            ObstacleTypeEnum obstacleType,
            bool isRideSupport)
        {
            if (IsCollectable(obstacleType))
                return Outcome.Collect;

            if (!ObstacleTypePolicy.IsPhysical(obstacleType))
                return Outcome.Ignore;

            if (!IsRoof(obstacleType))
                return Outcome.Ignore;

            return isRideSupport
                ? Outcome.PreserveSupport
                : Outcome.Destroy;
        }

        /// <summary>
        /// Возвращает результат контакта во время прыжка.
        /// </summary>
        public static Outcome DecideJump(
            ObstacleTypeEnum obstacleType,
            bool startedOnRoof)
        {
            if (IsCollectable(obstacleType))
                return Outcome.Collect;

            if (!ObstacleTypePolicy.IsPhysical(obstacleType))
                return Outcome.Ignore;

            return startedOnRoof && IsRoof(obstacleType)
                ? Outcome.PreserveSupport
                : Outcome.Destroy;
        }

        /// <summary>
        /// Возвращает результат контакта с landing wave.
        /// </summary>
        public static Outcome DecideLandingWave(
            ObstacleTypeEnum obstacleType,
            bool startedOnRoof,
            bool isCurrentSupport,
            bool isDestructionArea)
        {
            if (IsCollectable(obstacleType))
                return Outcome.BumpOnly;

            if (!ObstacleTypePolicy.IsPhysical(obstacleType))
                return Outcome.Ignore;

            bool isRoof = IsRoof(obstacleType);
            if (isRoof && isCurrentSupport)
                return Outcome.PreserveSupport;
            if (startedOnRoof && isRoof)
                return Outcome.BumpOnly;

            return isDestructionArea
                ? Outcome.Destroy
                : Outcome.BumpOnly;
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
        /// Возвращает true для obstacles, которые могут быть roof support.
        /// </summary>
        public static bool IsRoof(ObstacleTypeEnum obstacleType)
        {
            return CollisionUtils.IsRoofObstacle(obstacleType);
        }
    }
}
