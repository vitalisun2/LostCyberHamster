using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит проецированное состояние бота и мира для узла planning-дерева во время генерации и симуляции плана.
    /// </summary>
    public sealed class PlanningState
    {
        public PlanningState(HamsterSnapshot hamster, int nextObstacleIndex, float projectionWorldShift)
            : this(hamster, nextObstacleIndex, projectionWorldShift, null)
        {
        }

        public PlanningState(
            HamsterSnapshot hamster,
            int nextObstacleIndex,
            float projectionWorldShift,
            IEnumerable<int> removedObstacleInstanceIds)
        {
            Hamster = hamster;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
            RemovedObstacleInstanceIds = NormalizeRemovedObstacleInstanceIds(removedObstacleInstanceIds);
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
        /// Instance ids obstacles, которые уже были уничтожены внутри projected planning-ветки.
        /// </summary>
        public IReadOnlyList<int> RemovedObstacleInstanceIds { get; }

        /// <summary>
        /// Возвращает текущую линию хомяка в projected-состоянии planning.
        /// </summary>
        public bool IsOnBottomLine => Hamster.IsOnBottomLine;

        /// <summary>
        /// Возвращает true, если obstacle уже удалён в этой planning-ветке.
        /// </summary>
        public bool IsObstacleRemoved(int obstacleInstanceId)
        {
            for (int index = 0; index < RemovedObstacleInstanceIds.Count; index++)
            {
                if (RemovedObstacleInstanceIds[index] == obstacleInstanceId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает список удалённых obstacles с добавленным instance id без дублей.
        /// </summary>
        public IReadOnlyList<int> GetRemovedObstacleInstanceIdsWith(int? obstacleInstanceId)
        {
            if (!obstacleInstanceId.HasValue)
                return RemovedObstacleInstanceIds;

            if (IsObstacleRemoved(obstacleInstanceId.Value))
                return RemovedObstacleInstanceIds;

            var removedObstacleInstanceIds = new int[RemovedObstacleInstanceIds.Count + 1];
            for (int index = 0; index < RemovedObstacleInstanceIds.Count; index++)
                removedObstacleInstanceIds[index] = RemovedObstacleInstanceIds[index];

            removedObstacleInstanceIds[RemovedObstacleInstanceIds.Count] = obstacleInstanceId.Value;
            Array.Sort(removedObstacleInstanceIds);
            return removedObstacleInstanceIds;
        }

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
        /// Нормализует список удалённых obstacles для стабильного сравнения planning-state.
        /// </summary>
        private static IReadOnlyList<int> NormalizeRemovedObstacleInstanceIds(
            IEnumerable<int> removedObstacleInstanceIds)
        {
            if (removedObstacleInstanceIds == null)
                return Array.Empty<int>();

            var uniqueIds = new List<int>();
            foreach (int obstacleInstanceId in removedObstacleInstanceIds)
            {
                if (uniqueIds.Contains(obstacleInstanceId))
                    continue;

                uniqueIds.Add(obstacleInstanceId);
            }

            if (uniqueIds.Count == 0)
                return Array.Empty<int>();

            uniqueIds.Sort();
            return uniqueIds.ToArray();
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
