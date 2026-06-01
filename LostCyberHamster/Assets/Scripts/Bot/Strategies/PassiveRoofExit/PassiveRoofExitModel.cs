using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Хранит рассчитанные параметры пассивного схода с крыши.
    /// </summary>
    internal readonly struct PassiveRoofExitModel
    {
        public PassiveRoofExitModel(
            ObstacleSnapshot lastRoof,
            ObstacleSnapshot contextObstacle,
            int contextObstacleIndex,
            float exitStartShift,
            float completionWorldShift)
        {
            LastRoof = lastRoof;
            ContextObstacle = contextObstacle;
            ContextObstacleIndex = contextObstacleIndex;
            ExitStartShift = exitStartShift;
            CompletionWorldShift = completionWorldShift;
        }

        /// <summary>
        /// Последняя roof-платформа текущей passive roof chain.
        /// </summary>
        public ObstacleSnapshot LastRoof { get; }

        /// <summary>
        /// Obstacle, из-за которого planner рассматривает продолжение цепочки.
        /// </summary>
        public ObstacleSnapshot ContextObstacle { get; }

        /// <summary>
        /// Индекс context obstacle в projected snapshot.
        /// </summary>
        public int ContextObstacleIndex { get; }

        /// <summary>
        /// Сдвиг мира до начала runtime-схода с крыши.
        /// </summary>
        public float ExitStartShift { get; }

        /// <summary>
        /// Полный сдвиг мира до завершения runtime-схода в Run.
        /// </summary>
        public float CompletionWorldShift { get; }
    }
}
