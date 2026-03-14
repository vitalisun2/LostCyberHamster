using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Снимок состояния сцены в момент запуска pipeline.
    /// Строится SnapshotBuilder'ом — единственной точкой доступа к Unity-объектам.
    /// </summary>
    public class BotSceneSnapshot
    {
        /// <summary>Хомяк находится на нижней линии.</summary>
        public bool HamsterOnBottom;

        /// <summary>Хомяк находится в пространстве крыши.</summary>
        public bool HamsterOnRoof;

        /// <summary>Правый край хомяка в мировых координатах.</summary>
        public float HamsterRightX;

        /// <summary>Ширина коллайдера хомяка в мировых единицах.</summary>
        public float HamsterWidth;

        public int Energy;
        public int Lives;

        /// <summary>Time.time в момент построения снимка.</summary>
        public float SnapshotTime;

        /// <summary>
        /// Видимые объекты, отсортированные по LeftX.
        /// Category = Neutral по умолчанию — классификация выполняется ObjectClassifier'ом.
        /// </summary>
        public List<ObstacleInfo> VisibleObjects = new List<ObstacleInfo>();
    }
}
