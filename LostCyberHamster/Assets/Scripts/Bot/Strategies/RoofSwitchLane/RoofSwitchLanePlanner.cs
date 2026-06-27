using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Строит planning-модель смены линии между крышами.
    /// </summary>
    internal sealed class RoofSwitchLanePlanner
    {
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;

        public RoofSwitchLanePlanner(SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _fireWindowCalculator = fireWindowCalculator;
        }

        /// <summary>
        /// Возвращает model, если текущий roof-run state может безопасно перейти на roof другой линии.
        /// </summary>
        public bool TryBuildModel(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out RoofSwitchLaneModel model,
            out string deadEndReason)
        {
            // Проверяет общий вход и roof-run состояние.
            model = default;
            deadEndReason = null;
            if (planningState?.Hamster == null
                || worldSnapshot?.Obstacles == null
                || decisionPoint?.Chain == null
                || !CanSwitchFromRoof(planningState.Hamster))
            {
                return false;
            }

            // Выбирает context: текущая угроза или бонусная route-цель на другой линии.
            if (!TryResolveContext(
                    planningState,
                    decisionPoint,
                    out ObstacleSnapshot contextObstacle,
                    out int contextObstacleIndex,
                    out bool targetBottomLine,
                    out CollectibleObjectiveValue objectiveValue))
            {
                return false;
            }

            // Ограничивает запуск deadline-объектом.
            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    planningState.Hamster,
                    contextObstacle,
                    out float latestFireShift))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: deadline уже пройден.";
                return false;
            }

            if (!TryConstrainLatestFireShiftByCurrentRoofRun(
                    planningState,
                    worldSnapshot,
                    latestFireShift,
                    out latestFireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Находит безопасное окно, где целевая линия имеет roof support под хомяком.
            if (!_fireWindowCalculator.TrySelectRelevantFireWindowSample(
                    worldSnapshot,
                    planningState.Hamster,
                    targetBottomLine,
                    latestFireShift,
                    out SwitchLaneFireWindowSample fireWindowSample,
                    requireTargetRoofSupport: true))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: целевая roof-line недоступна.";
                return false;
            }

            // Фиксирует конкретную roof support, на которую придет runtime после tap.
            if (!_fireWindowCalculator.TryFindTargetRoofSupportAtFireShift(
                    worldSnapshot,
                    planningState.Hamster,
                    targetBottomLine,
                    fireWindowSample.FireShift,
                    out ObstacleSnapshot targetRoof)
                || !TryFindObstacleIndex(worldSnapshot, targetRoof, out int targetRoofIndex))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: target roof support не найден.";
                return false;
            }

            model = new RoofSwitchLaneModel(
                contextObstacle,
                contextObstacleIndex,
                targetRoof,
                targetRoofIndex,
                targetBottomLine,
                fireWindowSample,
                ResolveImmediateCollectibleObjective(
                    planningState.Hamster,
                    contextObstacle,
                    fireWindowSample.FireShift + SwitchLaneTiming.DecisionTravel,
                    objectiveValue));
            return true;
        }

        /// <summary>
        /// Проверяет состояние, в котором runtime принимает tap для roof switch-lane.
        /// </summary>
        private static bool CanSwitchFromRoof(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && hamster.RoofSupportInstanceId.HasValue
                && !hamster.IsShifting;
        }

        /// <summary>
        /// Ограничивает запуск текущей passive roof-chain: смещение должно завершиться до RunFromRoof.
        /// </summary>
        private static bool TryConstrainLatestFireShiftByCurrentRoofRun(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            float latestFireShift,
            out float constrainedLatestFireShift,
            out string deadEndReason)
        {
            constrainedLatestFireShift = latestFireShift;
            deadEndReason = null;
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null)
                return false;

            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out _))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: текущая roof-chain не найдена.";
                return false;
            }

            float roofExitStartShift = lastRoof.RightX
                + hamster.Width * RoofRunProjection.PassiveContinuationGapFactor
                - hamster.HamsterRightX;
            float latestBeforeRoofExit = roofExitStartShift - SwitchLaneTiming.DecisionTravel;
            if (latestBeforeRoofExit <= 0f)
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: текущая roof-chain закончится до завершения смещения.";
                return false;
            }

            if (latestBeforeRoofExit < constrainedLatestFireShift)
                constrainedLatestFireShift = latestBeforeRoofExit;

            return constrainedLatestFireShift > 0f;
        }

        /// <summary>
        /// Выбирает deadline context для defensive или reward roof-switch причины.
        /// </summary>
        private static bool TryResolveContext(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot contextObstacle,
            out int contextObstacleIndex,
            out bool targetBottomLine,
            out CollectibleObjectiveValue objectiveValue)
        {
            contextObstacle = null;
            contextObstacleIndex = -1;
            targetBottomLine = false;
            objectiveValue = CollectibleObjectiveValue.None;

            ObstacleChain chain = decisionPoint?.Chain;
            HamsterSnapshot hamster = planningState?.Hamster;
            if (chain == null || hamster == null)
                return false;

            if (chain.First.IsBottomLine == hamster.IsOnBottomLine)
            {
                if (!TryFindCurrentLaneThreatContext(chain, out contextObstacle, out contextObstacleIndex))
                    return false;

                targetBottomLine = !hamster.IsOnBottomLine;
                return true;
            }

            if (!TryFindPositiveCollectibleContext(
                    hamster,
                    chain,
                    out contextObstacle,
                    out contextObstacleIndex,
                    out objectiveValue))
            {
                return false;
            }

            targetBottomLine = chain.First.IsBottomLine;
            return true;
        }

        /// <summary>
        /// Ищет ближайшую current-lane угрозу, от которой нужно уйти на другую roof-line.
        /// </summary>
        private static bool TryFindCurrentLaneThreatContext(
            ObstacleChain chain,
            out ObstacleSnapshot contextObstacle,
            out int contextObstacleIndex)
        {
            contextObstacle = null;
            contextObstacleIndex = -1;
            if (chain == null)
                return false;

            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!element.HasRole(ObstacleRole.BlockingThreat)
                    && !element.HasRole(ObstacleRole.RoofOccupantHazard))
                {
                    continue;
                }

                contextObstacle = element.Obstacle;
                contextObstacleIndex = element.WorldIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ищет первый полезный collectable на opposite roof route.
        /// </summary>
        private static bool TryFindPositiveCollectibleContext(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out ObstacleSnapshot collectible,
            out int collectibleIndex,
            out CollectibleObjectiveValue objectiveValue)
        {
            collectible = null;
            collectibleIndex = -1;
            objectiveValue = CollectibleObjectiveValue.None;
            if (hamster == null || chain == null)
                return false;

            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!element.HasRole(ObstacleRole.Collectible))
                    continue;

                if (!CollectibleValuePolicy.TryGetPositiveValue(
                        hamster,
                        element.Obstacle,
                        out objectiveValue))
                {
                    continue;
                }

                collectible = element.Obstacle;
                collectibleIndex = element.WorldIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает collectable objective только если switch action сам доходит до pickup.
        /// </summary>
        private static CollectibleObjectiveValue ResolveImmediateCollectibleObjective(
            HamsterSnapshot hamster,
            ObstacleSnapshot contextObstacle,
            float completionWorldShift,
            CollectibleObjectiveValue objectiveValue)
        {
            if (hamster == null
                || contextObstacle == null
                || !objectiveValue.HasValue)
            {
                return CollectibleObjectiveValue.None;
            }

            float pickupShift = contextObstacle.LeftX - hamster.HamsterRightX;
            if (pickupShift < 0f)
                pickupShift = 0f;

            return pickupShift <= completionWorldShift
                ? objectiveValue
                : CollectibleObjectiveValue.None;
        }

        /// <summary>
        /// Находит world-index obstacle по instance id.
        /// </summary>
        private static bool TryFindObstacleIndex(
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            out int obstacleIndex)
        {
            obstacleIndex = -1;
            if (worldSnapshot?.Obstacles == null || obstacle == null)
                return false;

            for (int index = 0; index < worldSnapshot.Obstacles.Count; index++)
            {
                if (worldSnapshot.Obstacles[index].InstanceId != obstacle.InstanceId)
                    continue;

                obstacleIndex = index;
                return true;
            }

            return false;
        }
    }
}
