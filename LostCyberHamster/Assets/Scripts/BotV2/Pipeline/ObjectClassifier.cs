using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Классифицирует объекты снимка по категориям.
    /// Этап 3: Threat/Target/Collectible.
    /// Работает только со snapshot-данными, без обращения к Unity-объектам.
    /// </summary>
    public class ObjectClassifier
    {
        public void Classify(BotSceneSnapshot snapshot)
        {
            var list = snapshot.VisibleObjects;
            for (int i = 0; i < list.Count; i++)
            {
                var obs = list[i];
                var cat = ClassifyObject(obs.Type, obs.DistanceToHamster, snapshot.HamsterOnRoof);
                if (cat == obs.Category) continue;

                list[i] = new ObstacleInfo(
                    obs.Type, obs.IsTopLane,
                    obs.LeftX, obs.RightX, obs.CenterX,
                    obs.DistanceToHamster,
                    cat, obs.StableId);
            }
        }

        private static ObjectCategory ClassifyObject(ObstacleTypeEnum type, float distance, bool hamsterOnRoof)
        {
            // Объекты позади хомяка — нейтральны
            if (distance < -0.2f) return ObjectCategory.Neutral;

            switch (type)
            {
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                case ObstacleTypeEnum.collectableCrystal:
                case ObstacleTypeEnum.collectableLife:
                case ObstacleTypeEnum.collectableCoin:
                    return ObjectCategory.Collectible;

                case ObstacleTypeEnum.smallAlive:
                    return ObjectCategory.Target;

                case ObstacleTypeEnum.bigAlive:
                    return hamsterOnRoof ? ObjectCategory.Target : ObjectCategory.Threat;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return ObjectCategory.Threat;

                default:
                    return ObjectCategory.Neutral;
            }
        }
    }
}
