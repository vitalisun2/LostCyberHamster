using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Common
{
    /// <summary>
    /// Содержит общие категории gameplay-типов препятствий.
    /// </summary>
    public static class ObstacleTypePolicy
    {
        /// <summary>
        /// Возвращает true для физических препятствий, которые можно разрушить.
        /// </summary>
        public static bool IsPhysical(ObstacleTypeEnum obstacleType)
        {
            return obstacleType is ObstacleTypeEnum.smallAlive
                or ObstacleTypeEnum.bigAlive
                or ObstacleTypeEnum.smallNotAliveRoad
                or ObstacleTypeEnum.smallNotAliveRoadAndRoof
                or ObstacleTypeEnum.bigNotAlive
                or ObstacleTypeEnum.mediumNotAlive;
        }
    }
}
