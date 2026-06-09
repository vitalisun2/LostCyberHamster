using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит role-based decision points для выбранной focus lane.
    /// </summary>
    public sealed class DecisionPointDetector
    {
        /// <summary>
        /// Строит one-line role-based obstacle chain.
        /// </summary>
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();

        /// <summary>
        /// Пытается построить ближайшую role-based planning-ситуацию.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Проверяет planning state.
            decisionPoint = null;
            if (planningState?.Hamster == null)
                return false;

            // Делегирует detection на текущую линию хомяка.
            return TryDetect(
                planningState,
                worldSnapshot,
                planningState.IsOnBottomLine,
                out decisionPoint);
        }

        /// <summary>
        /// Пытается построить ближайшую role-based planning-ситуацию для выбранной focus lane.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            out DecisionPoint decisionPoint)
        {
            // Проверяет входные данные.
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Определяет индекс старта detection для focus lane.
            int firstDetectionIndex = GetFirstDetectionIndex(
                planningState,
                worldSnapshot,
                focusBottomLine);

            // Строит role-based chain для найденной ситуации.
            if (_chainBuilder.TryBuild(
                    planningState,
                    worldSnapshot,
                    firstDetectionIndex,
                    focusBottomLine,
                    out ObstacleChain chain))
            {
                decisionPoint = new DecisionPoint(chain);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает индекс obstacle, с которого нужно начинать detection.
        /// </summary>
        private static int GetFirstDetectionIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine)
        {
            // Берет обычный старт detection из planning state.
            int defaultDetectionIndex = planningState.NextObstacleIndex;
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.IsOnRoof)
                return defaultDetectionIndex;

            // На текущей roof lane сначала ищет occupant hazard на passive roof path.
            if (focusBottomLine == hamster.IsOnBottomLine
                && TryFindFirstRoofOccupantHazardIndex(
                    planningState,
                    worldSnapshot,
                    out int firstRoofOccupantHazardIndex))
            {
                return firstRoofOccupantHazardIndex;
            }

            // Если hazards нет, пропускает непрерывную passive roof chain.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out _,
                    out int lastRoofIndex))
            {
                return defaultDetectionIndex;
            }

            int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
            return firstIndexAfterPassiveRoofs > defaultDetectionIndex
                ? firstIndexAfterPassiveRoofs
                : defaultDetectionIndex;
        }

        /// <summary>
        /// Находит ближайший damaging occupant на текущем passive roof path.
        /// </summary>
        private static bool TryFindFirstRoofOccupantHazardIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out int firstHazardIndex)
        {
            // Проверяет наличие obstacles в snapshot.
            firstHazardIndex = -1;
            if (worldSnapshot?.Obstacles == null)
                return false;

            // Сканирует snapshot до первого damaging occupant.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        out _,
                        out _))
                {
                    continue;
                }

                firstHazardIndex = obstacleIndex;
                return true;
            }

            return false;
        }
    }
}
