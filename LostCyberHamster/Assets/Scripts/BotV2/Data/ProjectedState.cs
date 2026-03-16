using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Проецируемое состояние BotV2 после выполнения шага.
    /// Нужен для Stage 9 генерации двухшаговой цепочки без обращения к Unity-объектам.
    /// </summary>
    public class ProjectedState
    {
        public bool HamsterOnBottom;
        public float HamsterRightX;
        public float HamsterWidth;
        public int Energy;
        public List<ObstacleInfo> RemainingObjects = new List<ObstacleInfo>();

        /// <summary>
        /// Создаёт копию состояния для ветвления цепочек.
        /// </summary>
        public ProjectedState Clone()
        {
            return new ProjectedState
            {
                HamsterOnBottom = HamsterOnBottom,
                HamsterRightX = HamsterRightX,
                HamsterWidth = HamsterWidth,
                Energy = Energy,
                RemainingObjects = new List<ObstacleInfo>(RemainingObjects)
            };
        }
    }
}