using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Снимок состояния сцены в момент запуска pipeline.
    /// Строится SnapshotBuilder'ом — единственной точкой доступа к Unity-объектам.
    /// </summary>
    public class BotSceneSnapshot : BotStateBase
    {
        public float SnapshotTime;

        /// <summary>
        /// Видимые объекты, отсортированные по LeftX.
        /// Category = Neutral по умолчанию — классификация выполняется ObjectClassifier'ом.
        /// </summary>
        public List<ObstacleInfo> VisibleObjects = new List<ObstacleInfo>();

        /// <summary>
        /// Проверяет, находится ли препятствие на той же линии, что и хомяк.
        /// </summary>
        public bool IsOnSameLane(ObstacleInfo obstacle)
        {
            return HamsterOnBottom == !obstacle.IsTopLane;
        }
    }
}
