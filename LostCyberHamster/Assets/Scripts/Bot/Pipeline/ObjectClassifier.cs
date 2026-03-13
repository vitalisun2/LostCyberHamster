using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Классифицирует объекты снимка: проставляет Category и CollectiblePriority
    /// каждому ObstacleInfo в BotSceneSnapshot.VisibleObjects.
    /// Работает только со snapshot-данными, без обращения к Unity-объектам.
    /// </summary>
    public class ObjectClassifier
    {
        /// <summary>
        /// Проставляет Category и CollectiblePriority каждому объекту в snapshot.VisibleObjects.
        /// Классификация контекстно-зависима: учитывает текущее положение хомяка из snapshot.
        /// </summary>
        public void Classify(BotSceneSnapshot snapshot)
        {
            bool hamsterOnBottom = snapshot.HamsterOnBottom;
            bool hamsterOnRoof   = snapshot.HamsterOnRoof;
            var list = snapshot.VisibleObjects;

            for (int i = 0; i < list.Count; i++)
            {
                var obs = list[i];
                var category = ClassifyObject(obs.Type, obs.IsTopLane, obs.IsOnRoof,
                    hamsterOnBottom, hamsterOnRoof, obs.DistanceToHamster);
                var priority = GetCollectiblePriority(obs.Type, category);

                list[i] = new ObstacleInfo(
                    obs.Type, obs.LeftX, obs.RightX, obs.CenterX,
                    obs.IsTopLane, obs.IsOnRoof,
                    obs.DistanceToHamster, obs.TimeToReach,
                    category, obs.ObstacleRef, obs.StableId, priority);
            }
        }

        private static ObjectCategory ClassifyObject(
            ObstacleTypeEnum type, bool isTopLane, bool isOnRoof,
            bool hamsterOnBottom, bool hamsterOnRoof, float distance)
        {
            if (distance < -0.2f) return ObjectCategory.Neutral;

            switch (type)
            {
                case ObstacleTypeEnum.decor:
                    return ObjectCategory.Neutral;

                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                case ObstacleTypeEnum.collectableCrystal:
                case ObstacleTypeEnum.collectableLife:
                case ObstacleTypeEnum.collectableCoin:
                    return ObjectCategory.Bonus;

                case ObstacleTypeEnum.smallAlive:
                    // Target: можно напрыгнуть с дороги и с крыши
                    return ObjectCategory.Target;

                case ObstacleTypeEnum.bigAlive:
                    // С крыши — атакуем (Target), с дороги — обходим (Threat)
                    if (hamsterOnRoof)
                        return ObjectCategory.Target;
                    return ObjectCategory.Threat;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    // Угроза, но одновременно — потенциальная крыша для хомяка
                    return ObjectCategory.Threat;

                case ObstacleTypeEnum.smallNotAliveRoad:
                    return ObjectCategory.Threat;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return ObjectCategory.Threat;

                default:
                    return ObjectCategory.Neutral;
            }
        }

        /// <summary>
        /// Приоритет сбора для ObjectCategory.Bonus.
        /// Жизнь=4, Энергетик/Пицца=3, Кристалл=2, Монета=1, остальные=0.
        /// </summary>
        private static int GetCollectiblePriority(ObstacleTypeEnum type, ObjectCategory category)
        {
            if (category != ObjectCategory.Bonus) return 0;

            switch (type)
            {
                case ObstacleTypeEnum.collectableLife:      return 4;
                case ObstacleTypeEnum.collectableEnergetic: return 3;
                case ObstacleTypeEnum.collectablePizza:     return 3;
                case ObstacleTypeEnum.collectableCrystal:   return 2;
                case ObstacleTypeEnum.collectableCoin:      return 1;
                default:                                     return 0;
            }
        }
    }
}
