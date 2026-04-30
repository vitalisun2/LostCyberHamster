using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.SuperJumpOver.Models;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{

    internal static class SuperJumpOverChainCalculator
    {
        private const float Epsilon = 0.0001f;
        private const float LateFireBudget = 0.1f;

        /// <summary>
        /// Вычисляет окно суперпрыжка для цепочки препятствий, начиная с целевого obstacle.
        /// </summary>
        public static bool TryCalculate(
            HamsterSnapshot hamster,
            WorldSnapshot projectedWorldSnapshot,
            int targetObstacleIndex,
            float superJumpTravel,
            out SuperJumpOverChainModel window)
        {
            // Сбрасываем результат по умолчанию.
            window = default;

            // Проверяем обязательные входные данные.
            if (hamster == null
                || projectedWorldSnapshot == null
                || targetObstacleIndex < 0
                || targetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count)
            {
                return false;
            }

            // Проверяем, что целевое препятствие подходит для super jump-over.
            ObstacleSnapshot targetObstacle = projectedWorldSnapshot.Obstacles[targetObstacleIndex];
            if (!ObstacleClassifier.CanSuperJumpOverOnGround(targetObstacle.ObstacleType))
                return false;

            // Инициализируем цепочку по первому препятствию.
            bool isBottomLine = targetObstacle.IsBottomLine;
            float chainLeftX = targetObstacle.LeftX;
            float chainRightX = targetObstacle.RightX;
            int lastObstacleIndex = targetObstacleIndex;
            int obstacleCount = 1;

            // Строим начальное окно для первой obstacle.
            if (!TryGetOpenWindow(hamster, chainLeftX, chainRightX, superJumpTravel, out float firstFireShift, out float lastFireShift))
                return false;

            // Расширяем цепочку, пока для неё сохраняется общее окно.
            for (int obstacleIndex = targetObstacleIndex + 1; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.CanSuperJumpOverOnGround(obstacle.ObstacleType))
                    break;

                float candidateChainRightX = obstacle.RightX > chainRightX ? obstacle.RightX : chainRightX;
                if (!TryGetOpenWindow(
                        hamster,
                        chainLeftX,
                        candidateChainRightX,
                        superJumpTravel,
                        out float candidateFirstFireShift,
                        out float candidateLastFireShift))
                {
                    break;
                }

                chainRightX = candidateChainRightX;
                lastObstacleIndex = obstacleIndex;
                obstacleCount++;
                firstFireShift = candidateFirstFireShift;
                lastFireShift = candidateLastFireShift;
            }

            // Выбираем конкретный fire shift внутри найденного окна.
            if (!TrySelectFireShift(obstacleCount, firstFireShift, lastFireShift, out float selectedFireShift))
                return false;

            // Возвращаем рассчитанное окно цепочки.
            window = new SuperJumpOverChainModel(
                targetObstacleIndex,
                lastObstacleIndex,
                obstacleCount,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Вычисляет допустимое окно сдвига для активации суперпрыжка через цепочку препятствий.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            float chainLeftX,
            float chainRightX,
            float superJumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Вычисляем раннюю границу старта.
            firstFireShift = chainRightX - hamster.HamsterLeftX - superJumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Вычисляем позднюю границу старта.
            lastFireShift = chainLeftX - hamster.HamsterRightX;

            // Делаем окно строго открытым.
            firstFireShift += Epsilon;
            lastFireShift -= Epsilon;

            // Проверяем, осталось ли допустимое окно.
            bool hasOpenFireWindow = firstFireShift < lastFireShift;
            return hasOpenFireWindow;
        }

        /// <summary>
        /// Выбирает конкретный сдвиг активации суперпрыжка внутри допустимого окна.
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

            // Смещаем старт ближе к поздней границе для цепочки.
            fireShift = lastFireShift - LateFireBudget;

            // Проверяем, что выбранный сдвиг не вышел за раннюю границу.
            bool isSelectedFireShiftInsideWindow = fireShift > firstFireShift;
            return isSelectedFireShiftInsideWindow;
        }
    }
}
