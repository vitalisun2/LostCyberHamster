using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Вычисляет chain и fire window для roof jump-over.
    /// </summary>
    internal static class RoofJumpOverChainCalculator
    {
        public static bool TryCalculate(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            RoofJumpOverTravel travel,
            out RoofJumpOverChainModel model)
        {
            model = default;

            if (planningState == null || projectedWorldSnapshot == null || chain == null || chain.Count <= 0)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            // Находит первый roof hazard и проверяет, что он лежит на текущем roof path.
            if (!chain.TryGetAt(0, out ObstacleSnapshot firstHazard, out int firstHazardIndex)
                || !IsEligibleRoofHazard(planningState, projectedWorldSnapshot, firstHazard))
            {
                return false;
            }

            // Строит начальное окно по первому hazard.
            float chainLeftX = firstHazard.LeftX;
            float chainRightX = firstHazard.RightX;
            ObstacleSnapshot lastHazard = firstHazard;
            int lastHazardIndex = firstHazardIndex;
            int hazardCount = 1;

            if (!TryGetOpenWindow(
                    hamster,
                    chainLeftX,
                    chainRightX,
                    travel.RoofJumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Расширяет chain, пока вся группа hazards перелетает одним roof jump.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleSnapshot hazard, out int hazardWorldIndex))
                    return false;

                if (!IsEligibleRoofHazard(planningState, projectedWorldSnapshot, hazard))
                    break;

                float candidateChainRightX = hazard.RightX > chainRightX ? hazard.RightX : chainRightX;
                if (!TryGetOpenWindow(
                        hamster,
                        chainLeftX,
                        candidateChainRightX,
                        travel.RoofJumpTravel,
                        out float candidateFirstFireShift,
                        out float candidateLastFireShift))
                {
                    break;
                }

                chainRightX = candidateChainRightX;
                lastHazard = hazard;
                lastHazardIndex = hazardWorldIndex;
                hazardCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Выбирает точку запуска внутри итогового окна.
            if (!TrySelectFireShift(hazardCount, firstFireShift, lastFireShift, out float selectedFireShift))
                return false;

            model = new RoofJumpOverChainModel(
                firstHazard,
                firstHazardIndex,
                lastHazard,
                lastHazardIndex,
                hazardCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        private static bool IsEligibleRoofHazard(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot hazard)
        {
            if (hazard == null || hazard.ObstacleType != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                return false;

            if (hazard.IsBottomLine != planningState.Hamster.IsOnBottomLine)
                return false;

            return RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                planningState,
                projectedWorldSnapshot,
                hazard,
                out _,
                out _);
        }

        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float roofJumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            firstFireShift = chainRightX - hamster.HamsterLeftX - roofJumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = chainLeftX - hamster.HamsterRightX;

            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return firstFireShift < lastFireShift;
        }

        private static bool TrySelectFireShift(
            int hazardCount,
            float firstFireShift,
            float lastFireShift,
            out float fireShift)
        {
            if (hazardCount <= 1)
            {
                fireShift = (firstFireShift + lastFireShift) * 0.5f;
                return true;
            }

            fireShift = lastFireShift;
            return fireShift > firstFireShift;
        }
    }
}