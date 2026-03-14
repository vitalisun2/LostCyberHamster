using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Проецируемое состояние хомяка после выполнения одного или нескольких шагов.
    /// Используется ChainGenerator'ом для "симуляции" последствий без обращения к Unity.
    /// </summary>
    public class ProjectedState
    {
        /// <summary>Хомяк на нижней линии.</summary>
        public bool OnBottom;

        /// <summary>Хомяк находится на крыше.</summary>
        public bool OnRoof;

        /// <summary>Примерная X-позиция правого края хомяка после проекции.</summary>
        public float ApproxX;

        public float HamsterWidth;

        public int Energy;
        public int UltaCharge;

        /// <summary>Объекты, которые ещё не были обработаны цепочкой.</summary>
        public List<ObstacleInfo> RemainingObjects = new List<ObstacleInfo>();

        /// <summary>
        /// Создаёт начальное состояние из снимка сцены.
        /// </summary>
        public static ProjectedState FromSnapshot(BotSceneSnapshot snapshot)
        {
            return new ProjectedState
            {
                OnBottom    = snapshot.HamsterOnBottom,
                OnRoof      = snapshot.HamsterOnRoof,
                ApproxX     = snapshot.HamsterRightX,
                HamsterWidth = snapshot.HamsterWidth,
                Energy      = snapshot.Energy,
                UltaCharge  = snapshot.UltaCharge,
                RemainingObjects = new List<ObstacleInfo>(snapshot.VisibleObjects)
            };
        }

        /// <summary>
        /// Глубокая копия состояния — для ветвления при переборе вариантов цепочки.
        /// </summary>
        public ProjectedState Clone()
        {
            return new ProjectedState
            {
                OnBottom         = OnBottom,
                OnRoof           = OnRoof,
                ApproxX          = ApproxX,
                HamsterWidth     = HamsterWidth,
                Energy           = Energy,
                UltaCharge       = UltaCharge,
                RemainingObjects = new List<ObstacleInfo>(RemainingObjects)
            };
        }
    }
}
