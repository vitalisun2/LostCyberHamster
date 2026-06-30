using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Хранит выбранное окно запуска и roof support для смены линии между крышами.
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
        /// Возвращает roof support на целевой линии после смены линии.
        /// </summary>
        public ObstacleSnapshot TargetRoof { get; }

        /// <summary>
        /// Возвращает world-index target roof в исходном snapshot.
        /// </summary>
        public int TargetRoofIndex { get; }

        /// <summary>
        /// Возвращает выбранное окно запуска switch-lane.
        /// </summary>
        public SwitchLaneFireWindowSample FireWindowSample { get; }
    }
}
