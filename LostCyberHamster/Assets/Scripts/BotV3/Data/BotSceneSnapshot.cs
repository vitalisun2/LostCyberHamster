using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Снимок состояния сцены в момент запуска pipeline.
    /// Строится SnapshotBuilder'ом — единственной точкой доступа к Unity-объектам.
    /// </summary>
    public class BotSceneSnapshot
    {
        public bool HamsterOnBottom;
        public bool HamsterOnRoof;
        public float HamsterRightX;
        public float HamsterWidth;
        public int Energy;
        public int Lives;
        public float SnapshotTime;

        /// <summary>
        /// Видимые объекты, отсортированные по LeftX.
        /// Category = Neutral по умолчанию — классификация выполняется ObjectClassifier'ом.
        /// </summary>
        public List<ObstacleInfo> VisibleObjects = new List<ObstacleInfo>();
    }
}
