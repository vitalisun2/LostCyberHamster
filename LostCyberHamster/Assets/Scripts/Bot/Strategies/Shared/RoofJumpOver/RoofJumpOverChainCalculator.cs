using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Вычисляет covered roof-hazard chain и fire-window для roof jump-over.
    /// </summary>
    internal static class RoofJumpOverChainCalculator
    {
        /// <summary>
        /// Пытается вычислить covered hazard chain и окно запуска.
        /// </summary>
        public static bool TryCalculate(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            RoofJumpOverTravel travel,
            out RoofJumpOverChainModel model,
            out string deadEndReason)
        {
            // Проверяет вход и состояние хомяка.
            model = default;
            deadEndReason = null;
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
            if (!chain.TryGetAt(0, out ObstacleChainElement firstElement)
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
                    out float lastFireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Расширяет covered chain, пока hazards перелетаются одним roof jump.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleChainElement element))
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
                        out float candidateLastFireShift,
                        out _))
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
            {
                deadEndReason = "Safety margin не оставил безопасного окна для прыжка над препятствием на крыше.";
                return false;
            }

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
            ObstacleChainElement element)
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
            out float lastFireShift,
            out string deadEndReason)
        {
            // Считает границы по достижимости chain и контакту с первым hazard.
            deadEndReason = null;
            float rawFirstFireShift = chainRightX - hamster.HamsterLeftX - roofJumpTravel;
            if (rawFirstFireShift < 0f)
                rawFirstFireShift = 0f;

            float rawLastFireShift = chainLeftX - hamster.HamsterRightX;
            if (rawFirstFireShift >= rawLastFireShift)
            {
                firstFireShift = rawFirstFireShift;
                lastFireShift = rawLastFireShift;
                deadEndReason = "Нет безопасного окна для прыжка над препятствием на крыше: roof-jump не покрывает текущую hazard-chain.";
                return false;
            }

            // Сужает окно на общий safety margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift = rawFirstFireShift + fireWindowBoundaryMargin;
            lastFireShift = rawLastFireShift - fireWindowBoundaryMargin;
            if (firstFireShift >= lastFireShift)
            {
                deadEndReason = "Safety margin не оставил безопасного окна для прыжка над препятствием на крыше.";
                return false;
            }

            return true;
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
