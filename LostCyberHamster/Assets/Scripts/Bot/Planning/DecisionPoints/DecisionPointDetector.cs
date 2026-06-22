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
        /// Пытается построить ближайшую route-ситуацию, пропуская optional-only collectable chains.
        /// </summary>
        public bool TryDetectRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;
            if (planningState?.Hamster == null)
                return false;

            return TryDetectRoute(
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
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            int firstDetectionIndex = GetFirstDetectionIndex(
                planningState,
                worldSnapshot,
                focusBottomLine);

            return TryDetectFromIndex(
                planningState,
                worldSnapshot,
                focusBottomLine,
                firstDetectionIndex,
                requireRequiredRole: false,
                out decisionPoint);
        }

        /// <summary>
        /// Пытается построить ближайшую route-ситуацию для focus lane, не останавливаясь на optional-only collectables.
        /// </summary>
        public bool TryDetectRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            int firstDetectionIndex = GetFirstDetectionIndex(
                planningState,
                worldSnapshot,
                focusBottomLine);

            return TryDetectFromIndex(
                planningState,
                worldSnapshot,
                focusBottomLine,
                firstDetectionIndex,
                requireRequiredRole: true,
                out decisionPoint);
        }

        /// <summary>
        /// Строит ближайший decision point от заданного индекса; route mode пропускает optional-only chains.
        /// </summary>
        private bool TryDetectFromIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            int firstDetectionIndex,
            bool requireRequiredRole,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;
            int detectionIndex = firstDetectionIndex < 0 ? 0 : firstDetectionIndex;

            while (_chainBuilder.TryBuild(
                       planningState,
                       worldSnapshot,
                       detectionIndex,
                       focusBottomLine,
                       out ObstacleChain chain))
            {
                if (!requireRequiredRole || chain.HasAnyRequiredPlanningRole())
                {
                    decisionPoint = new DecisionPoint(chain);
                    return true;
                }

                int nextDetectionIndex = GetNextDetectionIndexAfter(chain);
                if (nextDetectionIndex <= detectionIndex)
                    return false;

                detectionIndex = nextDetectionIndex;
            }

            return false;
        }

        /// <summary>
        /// Возвращает индекс первого obstacle после chain.
        /// </summary>
        private static int GetNextDetectionIndexAfter(ObstacleChain chain)
        {
            if (chain == null || chain.Count == 0)
                return 0;

            return chain.Elements[chain.Count - 1].WorldIndex + 1;
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
            if (hamster != null && focusBottomLine != hamster.IsOnBottomLine)
                return 0;

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
