using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит прогнозируемое состояние бота внутри planning-дерева.
    /// </summary>
    public sealed class PlanningState
    {
        /// <summary>
        /// Создает одно planning-состояние для узла дерева решений.
        /// </summary>
        public PlanningState(HamsterSnapshot hamster, int nextObstacleIndex, float projectionWorldShift)
        {
            Hamster = hamster;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
        }

        public HamsterSnapshot Hamster { get; }
        public int NextObstacleIndex { get; }
        public float ProjectionWorldShift { get; }
        public bool IsOnBottomLine => Hamster.IsOnBottomLine;

        /// <summary>
        /// Создает корневое planning-состояние из snapshot мира.
        /// </summary>
        public static PlanningState FromSnapshot(WorldSnapshot worldSnapshot)
        {
            return new PlanningState(
                worldSnapshot.Hamster,
                FindInitialNextObstacleIndex(worldSnapshot),
                projectionWorldShift: 0f);
        }

        private static int FindInitialNextObstacleIndex(WorldSnapshot worldSnapshot)
        {
            if (worldSnapshot == null || worldSnapshot.Hamster == null)
                return 0;

            HamsterSnapshot hamster = worldSnapshot.Hamster;
            if (!hamster.IsOnRoof || !hamster.RoofSupportInstanceId.HasValue)
                return 0;

            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.InstanceId != hamster.RoofSupportInstanceId.Value)
                    continue;

                if (!IsCurrentRoofSupport(hamster, obstacle))
                    return 0;

                DebugManager.DiagLog(
                    $"[Bot PLAN] SKIP_ROOF_SUPPORT obstacle={obstacle.ObstacleType} " +
                    $"index={obstacleIndex} instanceId={obstacle.InstanceId} " +
                    $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2}");
                return obstacleIndex + 1;
            }

            return 0;
        }

        private static bool IsCurrentRoofSupport(HamsterSnapshot hamster, ObstacleSnapshot obstacle)
        {
            return obstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
        }
    }
}
