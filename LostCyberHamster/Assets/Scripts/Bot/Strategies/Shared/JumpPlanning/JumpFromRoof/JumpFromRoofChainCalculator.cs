using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Вычисляет chain и fire window для прыжка с крыши через дорожные obstacles.
    /// </summary>
    internal static class JumpFromRoofChainCalculator
    {
        /// <summary>
        /// Пытается вычислить obstacle chain и fire window для прыжка с крыши.
        /// </summary>
        public static bool TryCalculate(
            PlanningState planningState,
            ObstacleChain chain,
            JumpFromRoofTravel travel,
            out JumpFromRoofChainModel model)
        {
            // Инициализирует пустой результат.
            model = default;

            // Отсекает неполный вход.
            if (planningState == null || chain == null || chain.Count <= 0)
                return false;

            // Получает snapshot хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            // Проверяет первый obstacle chain.
            if (!chain.TryGetAt(0, out ObstacleSnapshot firstObstacle, out int firstObstacleIndex)
                || !IsEligibleObstacle(hamster, firstObstacle))
            {
                return false;
            }

            // Инициализирует границы chain.
            float chainLeftX = firstObstacle.LeftX;
            float chainRightX = firstObstacle.RightX;
            ObstacleSnapshot lastObstacle = firstObstacle;
            int lastObstacleIndex = firstObstacleIndex;
            int obstacleCount = 1;

            // Строит начальное fire window.
            if (!TryGetOpenWindow(
                    hamster,
                    chainLeftX,
                    chainRightX,
                    travel.ActionTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Расширяет chain, пока препятствия покрываются одним прыжком.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleSnapshot obstacle, out int obstacleWorldIndex))
                    return false;

                if (!IsEligibleObstacle(hamster, obstacle))
                    break;

                float candidateChainRightX = obstacle.RightX > chainRightX ? obstacle.RightX : chainRightX;
                if (!TryGetOpenWindow(
                        hamster,
                        chainLeftX,
                        candidateChainRightX,
                        travel.ActionTravel,
                        out float candidateFirstFireShift,
                        out float candidateLastFireShift))
                {
                    break;
                }

                chainRightX = candidateChainRightX;
                lastObstacle = obstacle;
                lastObstacleIndex = obstacleWorldIndex;
                obstacleCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Выбирает fire shift.
            if (!TrySelectFireShift(obstacleCount, firstFireShift, lastFireShift, out float selectedFireShift))
                return false;

            // Возвращает рассчитанную model.
            model = new JumpFromRoofChainModel(
                firstObstacle,
                firstObstacleIndex,
                lastObstacle,
                lastObstacleIndex,
                obstacleCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Проверяет, подходит ли obstacle для покрытия прыжком с крыши.
        /// </summary>
        private static bool IsEligibleObstacle(HamsterSnapshot hamster, ObstacleSnapshot obstacle)
        {
            if (obstacle == null)
                return false;

            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                return false;

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }

        /// <summary>
        /// Вычисляет открытое окно запуска для заданных границ chain.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float jumpFromRoofTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Считает левую границу fire window.
            firstFireShift = chainRightX - hamster.HamsterLeftX - jumpFromRoofTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Считает правую границу fire window.
            lastFireShift = chainLeftX - hamster.HamsterRightX;

            // Применяет margin только к ранней границе.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;

            // Проверяет, что окно не схлопнулось.
            return firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Выбирает точку запуска внутри рассчитанного fire window.
        /// </summary>
        private static bool TrySelectFireShift(
            int obstacleCount,
            float firstFireShift,
            float lastFireShift,
            out float fireShift)
        {
            // Выбирает самый поздний допустимый запуск.
            fireShift = lastFireShift;
            return fireShift > firstFireShift;
        }
    }
}
