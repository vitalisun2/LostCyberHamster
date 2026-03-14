using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Классифицирует объекты снимка по категориям.
    /// Этап 1: smallNotAliveRoad → Threat, всё остальное → Neutral.
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
                var cat = ClassifyObject(obs.Type, obs.DistanceToHamster);
                if (cat == obs.Category) continue;

                list[i] = new ObstacleInfo(
                    obs.Type, obs.IsTopLane,
                    obs.LeftX, obs.RightX, obs.CenterX,
                    obs.DistanceToHamster,
                    cat, obs.StableId);
            }
        }

        private static ObjectCategory ClassifyObject(ObstacleTypeEnum type, float distance)
        {
            // Объекты позади хомяка — нейтральны
            if (distance < -0.2f) return ObjectCategory.Neutral;

            switch (type)
            {
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return ObjectCategory.Threat;
                default:
                    return ObjectCategory.Neutral;
            }
        }
    }
}
