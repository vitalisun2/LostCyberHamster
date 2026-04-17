using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Дает planning-слою короткие factual-свойства типов препятствий.
    /// </summary>
    public static class ObstacleClassifier
    {
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
    }
}
