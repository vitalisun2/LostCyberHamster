using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Снимок данных об одном объекте на сцене.
    /// </summary>
    public readonly struct ObstacleInfo
    {
        public readonly ObstacleTypeEnum Type;
        public readonly float LeftX;
        public readonly float RightX;
        public readonly float CenterX;
        public readonly bool IsTopLane;
        public readonly bool IsOnRoof;
        public readonly float DistanceToHamster;
        public readonly float TimeToReach;
        public readonly ObjectCategory Category;
        /// <summary>Ссылка на реальный Obstacle для проверок через CollisionUtils.</summary>
        public readonly Obstacle ObstacleRef;
        /// <summary>
        /// Устойчивый идентификатор объекта (Obstacle.GetInstanceID).
        /// Используется PlanValidator'ом для проверки, что объект всё ещё на экране.
        /// </summary>
        public readonly int StableId;

        public ObstacleInfo(
            ObstacleTypeEnum type,
            float leftX, float rightX, float centerX,
            bool isTopLane, bool isOnRoof,
            float distanceToHamster, float timeToReach,
            ObjectCategory category,
            Obstacle obstacleRef,
            int stableId = 0)
        {
            Type = type;
            LeftX = leftX;
            RightX = rightX;
            CenterX = centerX;
            IsTopLane = isTopLane;
            IsOnRoof = isOnRoof;
            DistanceToHamster = distanceToHamster;
            TimeToReach = timeToReach;
            Category = category;
            ObstacleRef = obstacleRef;
            StableId = stableId;
        }
    }

    /// <summary>
    /// Категория объекта для принятия решений.
    /// </summary>
    public enum ObjectCategory
    {
        Target,     // Цель высокого приоритета (можно напрыгнуть)
        Bonus,      // Collectible (энергия, жизнь, монеты, кристалл)
        Threat,     // Угроза (столкновение = урон)
        Neutral     // Декор или объекты позади хомяка
    }
}
