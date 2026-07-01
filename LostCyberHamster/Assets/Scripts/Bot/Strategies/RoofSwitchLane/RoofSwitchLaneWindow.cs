using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Хранит выбранное окно запуска и результат посадки после смены линии с крыши.
    /// </summary>
    internal readonly struct RoofSwitchLaneWindow
    {
        public RoofSwitchLaneWindow(
            ObstacleSnapshot targetRoof,
            int targetRoofIndex,
            SwitchLaneFireWindowSample fireWindowSample)
        {
            TargetRoof = targetRoof;
            TargetRoofIndex = targetRoofIndex;
            FireWindowSample = fireWindowSample;
        }

        /// <summary>
        /// Возвращает roof support на целевой линии или null для посадки на дорогу.
        /// </summary>
        public ObstacleSnapshot TargetRoof { get; }

        /// <summary>
        /// Возвращает world-index target roof в исходном snapshot или -1 для посадки на дорогу.
        /// </summary>
        public int TargetRoofIndex { get; }

        /// <summary>
        /// Возвращает true, если смена линии завершится на roof support.
        /// </summary>
        public bool LandsOnRoof => TargetRoof != null;

        /// <summary>
        /// Возвращает выбранное окно запуска switch-lane.
        /// </summary>
        public SwitchLaneFireWindowSample FireWindowSample { get; }
    }
}
