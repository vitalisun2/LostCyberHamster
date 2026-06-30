using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит обязательные route decision points для текущей или противоположной линии.
    /// </summary>
    internal sealed class RouteDecisionPointDetector
    {
        /// <summary>
        /// Строит one-line role-based obstacle chain.
        /// </summary>
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();

        /// <summary>
        /// Пытается построить ближайшую обязательную route-ситуацию на текущей линии.
        /// </summary>
        public bool TryDetectCurrent(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Проверяет входной контекст.
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Делегирует detection на текущую линию.
            return TryDetectRoute(
                planningState,
                worldSnapshot,
                planningState.IsOnBottomLine,
                out decisionPoint);
        }

        /// <summary>
        /// Пытается построить ближайшую обязательную route-ситуацию на противоположной линии.
        /// </summary>
        public bool TryDetectOpposite(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Проверяет входной контекст.
            decisionPoint = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Делегирует detection на противоположную линию.
            return TryDetectRoute(
                planningState,
                worldSnapshot,
                !planningState.IsOnBottomLine,
                out decisionPoint);
        }

        /// <summary>
        /// Пытается построить ближайшую route-ситуацию для focus lane, не останавливаясь на optional-only collectables.
        /// </summary>
        private bool TryDetectRoute(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            out DecisionPoint decisionPoint)
        {
            // Выбирает старт detection для ground/roof state.
            int firstDetectionIndex = GetRouteStartIndex(
                planningState,
                worldSnapshot,
                focusBottomLine);

            // Строит route decision point.
            return TryDetectFromIndex(
                planningState,
                worldSnapshot,
                focusBottomLine,
                firstDetectionIndex,
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
                if (chain.HasAnyRequiredPlanningRole())
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
        /// Возвращает индекс obstacle, с которого нужно начинать route detection.
        /// </summary>
        private static int GetRouteStartIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine)
        {
            // Определяет focus lane.
            HamsterSnapshot hamster = planningState.Hamster;
            if (focusBottomLine != hamster.IsOnBottomLine)
                return 0;

            // Выбирает ground или roof правила старта.
            return hamster.IsOnRoof
                ? GetRoofRouteStartIndex(planningState, worldSnapshot)
                : GetGroundRouteStartIndex(planningState);
        }

        /// <summary>
        /// Возвращает обычный старт route detection на земле.
        /// </summary>
        private static int GetGroundRouteStartIndex(PlanningState planningState)
        {
            return planningState.NextObstacleIndex;
        }

        /// <summary>
        /// Возвращает roof-start: ближайший occupant hazard или первый obstacle после passive roof-chain.
        /// </summary>
        private static int GetRoofRouteStartIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            // На текущей roof lane сначала ищет occupant hazard на passive roof path.
            if (TryFindFirstRoofOccupantHazardIndex(
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
                return planningState.NextObstacleIndex;
            }

            int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
            return firstIndexAfterPassiveRoofs > planningState.NextObstacleIndex
                ? firstIndexAfterPassiveRoofs
                : planningState.NextObstacleIndex;
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
