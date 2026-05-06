using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.JumpOver.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    internal static class JumpOverChainCalculator
    {
        /// <summary>
        /// Вычисляет окно обычного прыжка для цепочки препятствий, начиная с целевого obstacle.
        /// </summary>
        public static bool TryCalculate(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            float jumpTravel,
            out JumpOverChainModel window)
        {
            // Сбрасываем результат по умолчанию.
            window = default;

            // Проверяем обязательные входные данные.
            if (hamster == null
                || chain == null
                || chain.Count <= 0)
            {
                return false;
            }

            // Проверяем, что целевое препятствие подходит для jump-over.
            ObstacleSnapshot targetObstacle = chain.FirstObstacle;
            if (!ObstacleClassifier.CanJumpOverOnGround(targetObstacle.ObstacleType))
                return false;

            // Инициализируем цепочку по первому препятствию.
            bool isBottomLine = targetObstacle.IsBottomLine;
            float chainLeftX = targetObstacle.LeftX;
            float chainRightX = targetObstacle.RightX;
            int firstObstacleIndex = chain.FirstIndex;
            int lastObstacleIndex = firstObstacleIndex;
            int obstacleCount = 1;

            // Строим начальное окно для первой obstacle.
            if (!TryGetOpenWindow(
                    hamster,
                    chainLeftX,
                    chainRightX,
                    jumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Расширяем цепочку, пока для неё сохраняется общее окно.
            for (int chainIndex = 1; chainIndex < chain.Count; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleSnapshot obstacle, out int obstacleWorldIndex))
                    return false;

                if (obstacle.IsBottomLine != isBottomLine)
                    return false;

                if (!ObstacleClassifier.CanJumpOverOnGround(obstacle.ObstacleType))
                    break;

                float candidateChainRightX = obstacle.RightX > chainRightX ? obstacle.RightX : chainRightX;
                if (!TryGetOpenWindow(
                        hamster,
                        chainLeftX,
                        candidateChainRightX,
                        jumpTravel,
                        out float candidateFirstFireShift,
                        out float candidateLastFireShift))
                {
                    break;
                }

                chainRightX = candidateChainRightX;
                lastObstacleIndex = obstacleWorldIndex;
                obstacleCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Выбираем конкретный fire shift внутри найденного окна.
            if (!TrySelectFireShift(obstacleCount, firstFireShift, lastFireShift, out float selectedFireShift))
                return false;

            // Возвращаем рассчитанное окно цепочки.
            window = new JumpOverChainModel(
                firstObstacleIndex,
                lastObstacleIndex,
                obstacleCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Вычисляет допустимое окно сдвига для активации обычного прыжка через цепочку препятствий.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Вычисляем раннюю границу старта.
            firstFireShift = chainRightX - hamster.HamsterLeftX - jumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Вычисляем позднюю границу старта.
            lastFireShift = chainLeftX - hamster.HamsterRightX;

            // Отступаем внутрь от обеих границ fire-window единым jump margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            // Проверяем, осталось ли допустимое окно.
            bool hasOpenFireWindow = firstFireShift < lastFireShift;
            return hasOpenFireWindow;
        }

        /// <summary>
        /// Выбирает конкретный сдвиг активации обычного прыжка внутри допустимого окна.
        /// </summary>
        private static bool TrySelectFireShift(
            int obstacleCount,
            float firstFireShift,
            float lastFireShift,
            out float fireShift)
        {
            // Выбираем середину окна для одиночного препятствия.
            if (obstacleCount <= 1)
            {
                fireShift = (firstFireShift + lastFireShift) * 0.5f;
                return true;
            }

            // Для цепочки выбираем позднюю границу уже суженного окна.
            fireShift = lastFireShift;

            // Проверяем, что выбранный сдвиг не вышел за раннюю границу.
            bool isSelectedFireShiftInsideWindow = fireShift > firstFireShift;
            return isSelectedFireShiftInsideWindow;
        }
    }
}
