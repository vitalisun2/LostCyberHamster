using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Предсказывает исход прыжка в контексте forward simulation.
    /// Чистая арифметика — никакого Unity API. Повторяет логику
    /// <see cref="BotJumpPredictor"/> / CollisionUtils на числах SimObstacle.
    /// </summary>
    public static class SimJumpPredictor
    {
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f;

        /// <summary>
        /// Предсказывает исход прыжка хомяка на конкретное препятствие.
        /// </summary>
        /// <param name="hamsterLeftX">Левая граница хомяка (фиксирована по X).</param>
        /// <param name="hamsterRightX">Правая граница хомяка.</param>
        /// <param name="hamsterCenterX">Центр хомяка по X.</param>
        /// <param name="hamsterWidth">Ширина коллайдера хомяка.</param>
        /// <param name="hamsterHeight">Высота коллайдера хомяка.</param>
        /// <param name="jumpShift">Горизонтальный сдвиг мира за время прыжка.</param>
        /// <param name="jumpMidY">Высота хомяка в середине прыжка (relative).</param>
        /// <param name="obstacle">Препятствие для проверки.</param>
        /// <returns>Предсказанный исход прыжка.</returns>
        public static JumpPrediction Predict(
            float hamsterLeftX,
            float hamsterRightX,
            float hamsterCenterX,
            float hamsterWidth,
            float hamsterHeight,
            float jumpShift,
            float jumpMidY,
            in SimObstacle obstacle)
        {
            switch (obstacle.Type)
            {
                case ObstacleTypeEnum.smallAlive:
                    return PredictSmallAlive(
                        hamsterLeftX, hamsterRightX, hamsterCenterX,
                        hamsterWidth, jumpShift, obstacle);

                case ObstacleTypeEnum.smallNotAliveRoad:
                    return PredictSmallNotAliveRoad(
                        hamsterLeftX, hamsterRightX, hamsterWidth, jumpShift, obstacle);

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    // Упрощение: в симуляции не проверяем, лежит ли на крыше.
                    // Если IsOnRoof — считаем как roof, иначе как road.
                    return PredictSmallNotAliveRoad(
                        hamsterLeftX, hamsterRightX, hamsterWidth, jumpShift, obstacle);

                case ObstacleTypeEnum.bigAlive:
                    return PredictBigAlive(
                        hamsterLeftX, hamsterRightX, hamsterCenterX,
                        hamsterHeight, jumpShift, jumpMidY, obstacle);

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return PredictRoofable(
                        hamsterLeftX, hamsterRightX, jumpShift, obstacle);

                default:
                    return JumpPrediction.NoHit;
            }
        }

        // ───── SmallAlive: напрыгнуть (бонус), damage, перелететь ─────

        private static JumpPrediction PredictSmallAlive(
            float hL, float hR, float hCenter,
            float hWidth, float jumpShift,
            in SimObstacle obs)
        {
            // Границы препятствия после сдвига мира
            float oL = obs.WorldLeftX - jumpShift;
            float oR = oL + obs.Width;

            float rightTol = hWidth * RIGHT_EDGE_TOL_RATIO;

            // 1. Центр хомяка внутри границ препятствия? → JumpOnObstacle
            if (hCenter >= oL && hCenter <= oR + rightTol)
                return JumpPrediction.JumpOnObstacle;

            // 2. Есть overlap? → Damage
            if (IsOverlap(hL, hR, oL, oR))
                return JumpPrediction.Damage;

            // 3. Перелетел? → JumpOver
            if (IsJumpOver(hL, hR, obs.WorldLeftX, obs.WorldLeftX + obs.Width, oL, oR))
                return JumpPrediction.JumpOver;

            return JumpPrediction.NoHit;
        }

        // ───── SmallNotAliveRoad: перепрыгнуть или damage ─────

        private static JumpPrediction PredictSmallNotAliveRoad(
            float hL, float hR, float hWidth,
            float jumpShift, in SimObstacle obs)
        {
            float oL = obs.WorldLeftX - jumpShift;
            float oR = oL + obs.Width;

            if (IsOverlap(hL, hR, oL, oR))
                return JumpPrediction.Damage;

            if (IsJumpOver(hL, hR, obs.WorldLeftX, obs.WorldLeftX + obs.Width, oL, oR))
                return JumpPrediction.JumpOver;

            return JumpPrediction.NoHit;
        }

        // ───── BigAlive: высокий, проверка по X и Y ─────

        private static JumpPrediction PredictBigAlive(
            float hL, float hR, float hCenter,
            float hHeight, float jumpShift, float jumpMidY,
            in SimObstacle obs)
        {
            float oL = obs.WorldLeftX - jumpShift;
            float oR = oL + obs.Width;

            // Проверка по X в конце клипа
            bool hitX = IsOverlap(hL, hR, oL, oR);

            // Проверка по Y в середине клипа (bigAlive высокий)
            float hCenterY = jumpMidY; // relative to ground
            float hHalf = hHeight * 0.5f;
            float hBottom = hCenterY - hHalf;
            float hTop = hCenterY + hHalf;
            bool hitY = IsOverlap(hBottom, hTop, 0f, obs.Height);

            if (hitX || hitY)
                return JumpPrediction.Damage;

            return JumpPrediction.NoHit;
        }

        // ───── Roofable (bigNotAlive, mediumNotAlive): запрыгнуть на крышу ─────

        private static JumpPrediction PredictRoofable(
            float hL, float hR,
            float jumpShift, in SimObstacle obs)
        {
            float oL = obs.WorldLeftX - jumpShift;
            float oR = oL + obs.Width;

            if (IsOverlap(hL, hR, oL, oR))
                return JumpPrediction.JumpOnRoof;

            return JumpPrediction.NoHit;
        }

        // ───── Helpers ─────

        /// <summary>Два интервала пересекаются?</summary>
        private static bool IsOverlap(float aL, float aR, float bL, float bR)
        {
            return aR > bL && bR > aL;
        }

        /// <summary>
        /// Хомяк перелетает через obstacle: до прыжка — правее хомяка,
        /// после прыжка — левее хомяка.
        /// </summary>
        private static bool IsJumpOver(
            float hL, float hR,
            float oStartL, float oStartR,
            float oEndL, float oEndR)
        {
            bool clearStart = hR < oStartL;  // до прыжка: правее хомяка
            bool clearEnd = hL > oEndR;       // после прыжка: левее хомяка
            bool noOverlap = !IsOverlap(hL, hR, oEndL, oEndR);
            return clearStart && clearEnd && noOverlap;
        }
    }
}
