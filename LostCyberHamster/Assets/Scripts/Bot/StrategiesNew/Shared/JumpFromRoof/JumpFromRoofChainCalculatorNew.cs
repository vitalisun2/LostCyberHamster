using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoof
{
    /// <summary>
    /// Вычисляет covered chain и fire-window для прыжка с крыши через дорожные threats.
    /// </summary>
    internal static class JumpFromRoofChainCalculatorNew
    {
        /// <summary>
        /// Пытается вычислить covered chain и окно запуска для прыжка с крыши.
        /// </summary>
        public static bool TryCalculate(
            IJumpFromRoofPolicy policy,
            PlanningState planningState,
            ObstacleChainNew chain,
            ObstacleSnapshot lastRoof,
            JumpFromRoofTravel travel,
            out JumpFromRoofChainModel model)
        {
            // Проверяет вход и состояние хомяка.
            model = default;
            if (policy == null
                || planningState?.Hamster == null
                || chain == null
                || lastRoof == null
                || chain.Count <= 0
                || travel.ActionTravel <= 0f)
            {
                return false;
            }

            // Проверяет первый obstacle chain.
            HamsterSnapshot hamster = planningState.Hamster;
            if (!chain.TryGetAt(0, out ObstacleChainElementNew firstElement)
                || !IsEligibleElement(hamster, firstElement))
            {
                return false;
            }

            // Инициализирует covered chain и roof-run deadline.
            ObstacleSnapshot firstObstacle = firstElement.Obstacle;
            float chainRightX = firstObstacle.RightX;
            ObstacleSnapshot lastObstacle = firstObstacle;
            int lastObstacleIndex = firstElement.WorldIndex;
            int obstacleCount = 1;
            float latestRoofRunFireShift =
                lastRoof.RightX
                + hamster.Width * RoofRunProjection.PassiveContinuationGapFactor
                - hamster.HamsterRightX;

            // Строит исходное fire-window.
            if (!TryGetOpenWindow(
                    hamster,
                    chainRightX,
                    latestRoofRunFireShift,
                    travel.ActionTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Расширяет covered chain, пока следующие threats покрываются тем же прыжком.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleChainElementNew element))
                    return false;

                if (!IsEligibleElement(hamster, element))
                    break;

                ObstacleSnapshot obstacle = element.Obstacle;
                float candidateChainRightX = obstacle.RightX > chainRightX
                    ? obstacle.RightX
                    : chainRightX;
                if (!TryGetOpenWindow(
                        hamster,
                        candidateChainRightX,
                        latestRoofRunFireShift,
                        travel.ActionTravel,
                        out float candidateFirstFireShift,
                        out float candidateLastFireShift))
                {
                    break;
                }

                chainRightX = candidateChainRightX;
                lastObstacle = obstacle;
                lastObstacleIndex = element.WorldIndex;
                obstacleCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Сужает окно с учетом bigAlive runtime-границ.
            ApplyBigAliveFireWindowLimits(
                policy,
                hamster,
                firstObstacle,
                lastObstacle,
                ref firstFireShift,
                ref lastFireShift);

            // Выбирает fire shift внутри окна.
            if (!TrySelectFireShift(firstFireShift, lastFireShift, out float selectedFireShift))
                return false;

            model = new JumpFromRoofChainModel(
                firstObstacle,
                firstElement.WorldIndex,
                lastObstacle,
                lastObstacleIndex,
                obstacleCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Проверяет, что element можно покрыть roof-to-road прыжком.
        /// </summary>
        private static bool IsEligibleElement(
            HamsterSnapshot hamster,
            ObstacleChainElementNew element)
        {
            if (hamster == null || element == null)
                return false;

            if (element.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (!element.HasRole(ObstacleRole.BlockingThreat))
                return false;

            if (element.HasRole(ObstacleRole.RoofSupport)
                || element.HasRole(ObstacleRole.RoofOccupantHazard))
            {
                return false;
            }

            return ObstacleClassifier.DamagesOnGroundContact(element.Obstacle.ObstacleType);
        }

        /// <summary>
        /// Вычисляет открытое fire-window для заданной правой границы covered chain.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainRightX,
            float latestRoofRunFireShift,
            float jumpFromRoofTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Считает левую границу по достижимости правого края covered chain.
            firstFireShift = chainRightX - hamster.HamsterLeftX - jumpFromRoofTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Применяет общий boundary margin к обеим границам.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift = latestRoofRunFireShift - fireWindowBoundaryMargin;

            return firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Сужает fire-window для bigAlive до safe runtime-границ.
        /// </summary>
        private static void ApplyBigAliveFireWindowLimits(
            IJumpFromRoofPolicy policy,
            HamsterSnapshot hamster,
            ObstacleSnapshot firstObstacle,
            ObstacleSnapshot lastObstacle,
            ref float firstFireShift,
            ref float lastFireShift)
        {
            // Не допускает поздний старт уже в контакте с первым bigAlive.
            if (firstObstacle.ObstacleType == ObstacleTypeEnum.bigAlive)
            {
                float fireWindowBoundaryMargin =
                    JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
                float latestBeforeFirstBigAlive =
                    firstObstacle.LeftX - hamster.HamsterRightX - fireWindowBoundaryMargin;

                if (latestBeforeFirstBigAlive < lastFireShift)
                    lastFireShift = latestBeforeFirstBigAlive;
            }

            // Применяет policy padding для runtime-порога bigAlive collision.
            float padding = hamster.Width * policy.BigAliveCollisionPaddingRatio;
            if (padding <= 0f)
                return;

            if (firstObstacle.ObstacleType == ObstacleTypeEnum.bigAlive)
                lastFireShift -= padding;

            if (lastObstacle.ObstacleType == ObstacleTypeEnum.bigAlive)
                firstFireShift += padding;
        }

        /// <summary>
        /// Выбирает середину рассчитанного окна.
        /// </summary>
        private static bool TrySelectFireShift(
            float firstFireShift,
            float lastFireShift,
            out float fireShift)
        {
            fireShift = (firstFireShift + lastFireShift) * 0.5f;
            return fireShift > firstFireShift;
        }
    }
}
