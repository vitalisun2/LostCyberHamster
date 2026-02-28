using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Информация о сканированном препятствии/коллектибле перед хомяком.
    /// </summary>
    public struct ThreatInfo
    {
        /// <summary>Ссылка на реальный Obstacle.</summary>
        public Obstacle Obstacle;

        /// <summary>Тип препятствия.</summary>
        public ObstacleTypeEnum Type;

        /// <summary>Расстояние от правого края хомяка до левого края препятствия (юниты).</summary>
        public float DistanceX;

        /// <summary>Секунд до столкновения (DistanceX / GameSpeedBase).</summary>
        public float TimeToReach;

        /// <summary>Препятствие на текущей линии хомяка.</summary>
        public bool IsOnCurrentLane;

        /// <summary>Препятствие на противоположной линии.</summary>
        public bool IsOnOtherLane;

        /// <summary>Можно подобрать (coin, crystal, energetic, pizza, life).</summary>
        public bool IsCollectable;

        /// <summary>Маленькое живое — можно напрыгнуть для бонуса.</summary>
        public bool IsSmallAlive;

        /// <summary>Можно забежать на крышу (bigNotAlive, mediumNotAlive).</summary>
        public bool IsRoofable;

        /// <summary>Может нанести урон при столкновении.</summary>
        public bool IsDangerous;

        public override string ToString()
        {
            return $"{Type}@{DistanceX:F1} ({TimeToReach:F2}s)" +
                   (IsOnCurrentLane ? " [cur]" : " [other]") +
                   (IsDangerous ? " DANGER" : "") +
                   (IsCollectable ? " COLLECT" : "") +
                   (IsSmallAlive ? " JUMPON" : "") +
                   (IsRoofable ? " ROOF" : "");
        }
    }
}
