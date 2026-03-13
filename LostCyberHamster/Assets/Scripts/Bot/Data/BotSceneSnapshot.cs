using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Снимок состояния сцены в момент планирования.
    /// Создаётся SnapshotBuilder'ом — единственной точкой доступа к Unity-объектам.
    /// </summary>
    public class BotSceneSnapshot
    {
        /// <summary>Хомяк находится на нижней линии.</summary>
        public bool HamsterOnBottom;

        /// <summary>Хомяк находится в пространстве крыши (на вершине машины/автобуса).</summary>
        public bool HamsterOnRoof;

        /// <summary>Правый край хомяка в мировых координатах (для расчёта расстояний).</summary>
        public float HamsterRightX;

        public int Energy;
        public int Lives;
        public int UltaCharge;
        public int Coins;

        /// <summary>
        /// Видимые объекты сцены. Category = Neutral по умолчанию —
        /// классификация выполняется отдельным компонентом (ObjectClassifier).
        /// </summary>
        public List<ObstacleInfo> VisibleObjects = new List<ObstacleInfo>();
    }
}
