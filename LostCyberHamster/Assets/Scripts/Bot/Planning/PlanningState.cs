using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит проецированное состояние бота и мира для узла planning-дерева во время генерации и симуляции плана.
    /// </summary>
    public sealed class PlanningState
    {
        public PlanningState(HamsterSnapshot hamster, int nextObstacleIndex, float projectionWorldShift)
        {
            Hamster = hamster;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
        }

        /// <summary>
        /// Содержит snapshot хомяка в текущем projected-состоянии planning.
        /// </summary>
        public HamsterSnapshot Hamster { get; }

        /// <summary>
        /// Указывает индекс ближайшего obstacle, с которого нужно продолжать поиск decision point.
        /// </summary>
        public int NextObstacleIndex { get; }

        /// <summary>
        /// Хранит накопленный сдвиг мира, который переводит live-координаты в projected-координаты planning.
        /// </summary>
        public float ProjectionWorldShift { get; }

        /// <summary>
        /// Возвращает текущую линию хомяка в projected-состоянии planning.
        /// </summary>
        public bool IsOnBottomLine => Hamster.IsOnBottomLine;

        /// <summary>
        /// Создаёт корневое planning-состояние из live snapshot мира перед началом построения плана.
        /// </summary>
        public static PlanningState FromSnapshot(WorldSnapshot worldSnapshot)
        {
            // Инициализирует корневое состояние по текущему snapshot мира.
            return new PlanningState(
                worldSnapshot.Hamster,
                FindInitialNextObstacleIndex(worldSnapshot),
                projectionWorldShift: 0f);
        }

        /// <summary>
        /// Определяет стартовый индекс obstacle, который planning должен рассматривать первым.
        /// </summary>
        private static int FindInitialNextObstacleIndex(WorldSnapshot worldSnapshot)
        {
            // Отсекает snapshot, в котором нечего анализировать.
            if (worldSnapshot == null || worldSnapshot.Hamster == null)
                return 0;

            // Проверяет, нужно ли пропустить obstacle, по крыше которого хомяк уже бежит.
            HamsterSnapshot hamster = worldSnapshot.Hamster;
            if (!hamster.IsOnRoof || !hamster.RoofSupportInstanceId.HasValue)
                return 0;

            // Ищет текущее roof support obstacle в live snapshot.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.InstanceId != hamster.RoofSupportInstanceId.Value)
                    continue;

                if (!IsCurrentRoofSupport(hamster, obstacle))
                    return 0;

                // Логирует пропуск текущей опоры крыши и возвращает следующий obstacle.
                DebugManager.DiagLogVerbose(
                    $"[Bot PLAN] SKIP_ROOF_SUPPORT obstacle={obstacle.ObstacleType} " +
                    $"index={obstacleIndex} instanceId={obstacle.InstanceId} " +
                    $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2}");
                return obstacleIndex + 1;
            }

            // Оставляет старт с первого obstacle, если опора не найдена.
            return 0;
        }

        /// <summary>
        /// Проверяет, что obstacle является текущей опорой крыши для projected-состояния хомяка.
        /// </summary>
        private static bool IsCurrentRoofSupport(HamsterSnapshot hamster, ObstacleSnapshot obstacle)
        {
            return obstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
        }
    }
}
