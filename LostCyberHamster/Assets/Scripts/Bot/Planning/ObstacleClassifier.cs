using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Дает planning-слою короткие factual-свойства типов препятствий.
    /// </summary>
    public static class ObstacleClassifier
    {
        /// <summary>
        /// Возвращает true, если obstacle имеет собственную крышу, по которой может бежать хомяк.
        /// </summary>
        public static bool IsObstacleWithRoof(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.bigNotAlive
                || obstacleType == ObstacleTypeEnum.mediumNotAlive;
        }

        /// <summary>
        /// Возвращает true, если объект опасен при прямом контакте на земле.
        /// </summary>
        public static bool DamagesOnGroundContact(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.smallAlive
                || obstacleType == ObstacleTypeEnum.bigAlive
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoad
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof
                || obstacleType == ObstacleTypeEnum.bigNotAlive
                || obstacleType == ObstacleTypeEnum.mediumNotAlive;
        }

        /// <summary>
        /// Возвращает true, если препятствие можно перепрыгнуть обычным ground-jump.
        /// </summary>
        public static bool CanJumpOverOnGround(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.smallAlive
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoad
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof;
        }

        /// <summary>
        /// Возвращает true, если препятствие можно перепрыгнуть ground-super-jump.
        /// </summary>
        public static bool CanSuperJumpOverOnGround(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.bigAlive
                || obstacleType == ObstacleTypeEnum.smallAlive
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoad
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof;
        }

        /// <summary>
        /// Возвращает true, если препятствие можно уничтожить ground jump-on действием.
        /// </summary>
        public static bool CanJumpOnGroundObstacle(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.smallAlive;
        }

        /// <summary>
        /// Возвращает true для дорожных small-obstacle типов, которые можно перелететь одной chain-over дугой.
        /// </summary>
        public static bool IsRoadSmallOverChainObstacle(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.smallNotAliveRoad
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof;
        }
    }
}
