using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Определяет planner-категорию obstacle по его типу и текущему snapshot-контексту.
    /// </summary>
    public class ObjectClassifier
    {
        private const float BehindHamsterThreshold = -0.2f;

        public bool TryGetCategory(
            ObstacleInfo obstacle,
            BotSceneSnapshot snapshot,
            out ObjectCategory category)
        {
            category = default;
            if (snapshot == null)
                return false;
            if (obstacle.DistanceToHamster < BehindHamsterThreshold)
                return false;

            switch (obstacle.Type)
            {
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                case ObstacleTypeEnum.collectableCrystal:
                case ObstacleTypeEnum.collectableLife:
                case ObstacleTypeEnum.collectableCoin:
                    category = ObjectCategory.Collectible;
                    return true;

                case ObstacleTypeEnum.smallAlive:
                    category = ObjectCategory.Target;
                    return true;

                case ObstacleTypeEnum.bigAlive:
                    category = snapshot.HamsterOnRoof ? ObjectCategory.Target : ObjectCategory.Threat;
                    return true;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    category = ObjectCategory.Threat;
                    return true;

                default:
                    return false;
            }
        }

        public bool IsThreat(ObstacleInfo obstacle, BotSceneSnapshot snapshot)
        {
            return TryGetCategory(obstacle, snapshot, out ObjectCategory category)
                && category == ObjectCategory.Threat;
        }
    }
}
