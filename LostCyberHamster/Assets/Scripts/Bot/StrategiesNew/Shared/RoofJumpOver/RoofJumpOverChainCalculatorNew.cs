using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.RoofJumpOver
{
    /// <summary>
    /// Вычисляет covered roof-hazard chain и fire-window для roof jump-over.
    /// </summary>
    internal static class RoofJumpOverChainCalculatorNew
    {
        /// <summary>
        /// Пытается вычислить covered hazard chain и окно запуска.
        /// </summary>
        public static bool TryCalculate(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChainNew chain,
            RoofJumpOverTravel travel,
            out RoofJumpOverChainModel model)
        {
            // Проверяет вход и состояние хомяка.
            model = default;
            if (planningState?.Hamster == null
                || projectedWorldSnapshot == null
                || chain == null
                || chain.Count <= 0
                || travel.RoofJumpTravel <= 0f)
            {
                return false;
            }

            // Находит первый hazard и проверяет, что он лежит на текущем roof path.
            HamsterSnapshot hamster = planningState.Hamster;
            if (!chain.TryGetAt(0, out ObstacleChainElementNew firstElement)
                || !IsEligibleRoofHazard(planningState, projectedWorldSnapshot, firstElement))
            {
                return false;
            }

            // Строит начальное окно по первому hazard.
            ObstacleSnapshot firstHazard = firstElement.Obstacle;
            float chainLeftX = firstHazard.LeftX;
            float chainRightX = firstHazard.RightX;
            ObstacleSnapshot lastHazard = firstHazard;
            int lastHazardIndex = firstElement.WorldIndex;
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

            // Расширяет covered chain, пока hazards перелетаются одним roof jump.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleChainElementNew element))
                    return false;

                if (!IsEligibleRoofHazard(planningState, projectedWorldSnapshot, element))
                    break;

                ObstacleSnapshot hazard = element.Obstacle;
                float candidateChainRightX = hazard.RightX > chainRightX
                    ? hazard.RightX
                    : chainRightX;
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
                lastHazardIndex = element.WorldIndex;
                hazardCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Выбирает fire shift внутри итогового окна.
            if (!TrySelectFireShift(hazardCount, firstFireShift, lastFireShift, out float selectedFireShift))
                return false;

            model = new RoofJumpOverChainModel(
                firstHazard,
                firstElement.WorldIndex,
                lastHazard,
                lastHazardIndex,
                hazardCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Проверяет, что element является damaging occupant на текущем passive roof path.
        /// </summary>
        private static bool IsEligibleRoofHazard(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChainElementNew element)
        {
            if (element == null || !element.HasRole(ObstacleRole.RoofOccupantHazard))
                return false;

            return RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                planningState,
                projectedWorldSnapshot,
                element.Obstacle,
                out _,
                out _);
        }

        /// <summary>
        /// Вычисляет открытое fire-window для текущих границ covered hazard chain.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float roofJumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Считает границы по достижимости chain и контакту с первым hazard.
            firstFireShift = chainRightX - hamster.HamsterLeftX - roofJumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = chainLeftX - hamster.HamsterRightX;

            // Сужает окно на общий safety margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            return firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Выбирает точку запуска: середину для одного hazard, позднюю границу для группы.
        /// </summary>
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
