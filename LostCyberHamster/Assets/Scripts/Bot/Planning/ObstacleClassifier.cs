using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит planning-классификацию типов препятствий по их физическим свойствам.
    /// </summary>
    public static class ObstacleClassifier
    {
        /// <summary>
        /// Возвращает true, если столкновение с препятствием на земле наносит урон.
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
    }
}
