using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Классифицирует объекты снимка по категориям: Threat/Target/Collectible/Neutral.
    /// Работает только со snapshot-данными, без обращения к Unity-объектам.
    /// </summary>
    public class ObjectClassifier
    {
        /// <summary>
        /// Создаёт новый snapshot с классифицированными объектами.
        /// </summary>
        public BotSceneSnapshot Classify(BotSceneSnapshot snapshot)
        {
            var classifiedSnapshot = CreateSnapshotShell(snapshot, snapshot.VisibleObjects.Count);
            var source = snapshot.VisibleObjects;

            for (int i = 0; i < source.Count; i++)
            {
                var obs = source[i];
                var cat = ClassifyObject(obs.Type, obs.DistanceToHamster, snapshot.HamsterOnRoof);
                classifiedSnapshot.VisibleObjects.Add(cat == obs.Category ? obs : WithCategory(obs, cat));
            }

            return classifiedSnapshot;
        }

        private const float BehindHamsterThreshold = -0.2f;

        private static BotSceneSnapshot CreateSnapshotShell(BotSceneSnapshot source, int visibleObjectsCapacity)
        {
            var snapshot = new BotSceneSnapshot();
            snapshot.CopyFrom(source);
            snapshot.SnapshotTime = source.SnapshotTime;
            snapshot.VisibleObjects = new List<ObstacleInfo>(visibleObjectsCapacity);
            return snapshot;
        }

        private static ObstacleInfo WithCategory(ObstacleInfo obstacle, ObjectCategory category)
        {
            return new ObstacleInfo(
                obstacle.Type,
                obstacle.IsTopLane,
                obstacle.LeftX,
                obstacle.RightX,
                obstacle.CenterX,
                obstacle.DistanceToHamster,
                category,
                obstacle.StableId);
        }

        private static ObjectCategory ClassifyObject(ObstacleTypeEnum type, float distance, bool hamsterOnRoof)
        {
            if (distance < BehindHamsterThreshold) return ObjectCategory.Neutral;

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
