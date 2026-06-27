using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.PassiveCollect
{
    /// <summary>
    /// Строит planning-модель пассивного подбора полезного collectable.
    /// </summary>
    internal static class PassiveCollectPlanner
    {
        private const float VerticalOverlapEpsilon = 0.0001f;

        /// <summary>
        /// Возвращает модель passive collect, если collectable можно безопасно подобрать без input.
        /// </summary>
        public static bool TryBuildModel(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out PassiveCollectModel model)
        {
            // Проверяет состояние и decision point.
            model = default;
            if (planningState?.Hamster == null
                || worldSnapshot == null
                || decisionPoint?.Chain == null
                || !CanCollectPassively(planningState.Hamster))
            {
                return false;
            }

            // Выбирает первый ценный collectable на текущей линии.
            if (!TryFindCollectible(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    out ObstacleSnapshot targetCollectible,
                    out int targetCollectibleIndex,
                    out CollectibleObjectiveValue objectiveValue))
            {
                return false;
            }

            // Проверяет, что pickup не требует входа на другую линию.
            if (targetCollectible.IsBottomLine != planningState.Hamster.IsOnBottomLine)
                return false;

            // Проверяет safety до pickup и возвращает модель.
            float completionWorldShift = CalculatePickupShift(
                planningState.Hamster,
                targetCollectible);
            if (!PassiveCollectSafety.IsSafeUntilPickup(
                    planningState,
                    worldSnapshot,
                    targetCollectible,
                    completionWorldShift))
            {
                return false;
            }

            model = new PassiveCollectModel(
                targetCollectible,
                targetCollectibleIndex,
                completionWorldShift,
                objectiveValue);
            return true;
        }

        private static bool CanCollectPassively(HamsterSnapshot hamster)
        {
            return hamster != null
                && !hamster.IsShifting
                && (hamster.HamsterState == HamsterStateEnum.Run
                    || hamster.HamsterState == HamsterStateEnum.RoofRun
                    || hamster.HamsterState == HamsterStateEnum.RunFromRoof);
        }

        private static bool TryFindCollectible(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleChain chain,
            out ObstacleSnapshot targetCollectible,
            out int targetCollectibleIndex,
            out CollectibleObjectiveValue objectiveValue)
        {
            targetCollectible = null;
            targetCollectibleIndex = -1;
            objectiveValue = CollectibleObjectiveValue.None;
            HamsterSnapshot hamster = planningState?.Hamster;

            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!element.HasRole(ObstacleRole.Collectible))
                    continue;

                if (!CanReachCollectiblePassively(planningState, worldSnapshot, element.Obstacle))
                    continue;

                if (!CollectibleValuePolicy.TryGetPositiveValue(
                        hamster,
                        element.Obstacle,
                        out objectiveValue))
                {
                    continue;
                }

                targetCollectible = element.Obstacle;
                targetCollectibleIndex = element.WorldIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, можно ли добраться до collectable без дополнительного input из текущего состояния.
        /// </summary>
        private static bool CanReachCollectiblePassively(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot collectible)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null || collectible == null)
                return false;

            if (hamster.HamsterState == HamsterStateEnum.RoofRun && hamster.IsOnRoof)
            {
                return RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                    planningState,
                    worldSnapshot,
                    collectible,
                    out _,
                    out _);
            }

            return HasVerticalOverlap(hamster, collectible);
        }

        /// <summary>
        /// Возвращает true, если текущая collider-высота хомяка пересекает collectable.
        /// </summary>
        private static bool HasVerticalOverlap(
            HamsterSnapshot hamster,
            ObstacleSnapshot collectible)
        {
            if (hamster == null || collectible == null)
                return false;

            return collectible.TopY >= hamster.HamsterBottomY - VerticalOverlapEpsilon
                && collectible.BottomY <= hamster.HamsterTopY + VerticalOverlapEpsilon;
        }

        private static float CalculatePickupShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetCollectible)
        {
            float pickupShift = targetCollectible.LeftX - hamster.HamsterRightX;
            return pickupShift > 0f ? pickupShift : 0f;
        }
    }
}
