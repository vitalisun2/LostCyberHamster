using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Хранит рассчитанные параметры role-based пассивного схода с крыши.
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
        /// Obstacle, относительно которого graph рассматривает следующий planning context.
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
