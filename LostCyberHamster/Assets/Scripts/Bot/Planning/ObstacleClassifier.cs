using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    public static class ObstacleClassifier
    {
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
