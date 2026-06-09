using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Определяет итог обычного прыжка с крыши по тем же правилам, что и runtime roof-jump механика.
    /// Resolver не работает с Unity-объектами напрямую: вызывающий код передает снимок препятствий
    /// и геометрию хомяка через <see cref="JumpObstacleData"/> и <see cref="RoofJumpResolveContext"/>.
    /// </summary>
    public static class RoofJumpOutcomeResolver
    {
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // 20 % ширины хомяка
        private const int NoTarget = -1;

        /// <summary>
        /// Обходит препятствия впереди хомяка и через обработчик по типу препятствия
        /// определяет итог roof jump: прыжок по крыше, сход с крыши, урон или напрыг.
        /// </summary>
        public static JumpResolveResult ResolveRoofJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            JumpResolveResult noHit = new(HamsterStateEnum.JumpFromRoof, NoTarget);

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != context.IsBottomLine)
                    continue;

                if (obstacle.CenterX <= context.HamsterCenterX)
                    continue;

                // Корректный ранний выход по левой грани препятствия.
                if (ShouldBreakByReachRight(context, obstacle))
                {
                    LogBreak(obstacleIndex, obstacle, context);
                    break;
                }

                LogCandidate(obstacleIndex, obstacle, context);

                JumpResolveResult result = HandleObstacle(
                    obstacle,
                    obstacleIndex,
                    obstacles,
                    context,
                    noHit);
                LogResult(obstacleIndex, obstacle, result, noHit, context);

                if (result.State != noHit.State)
                    return result;
            }

            return noHit;
        }

        private static JumpResolveResult HandleObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            switch (obstacle.Type)
            {
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return HandleBigNotAlive(obstacle, obstacleIndex, obstacles, context, noHit);
                case ObstacleTypeEnum.bigAlive:
                    return HandleBigAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallAlive:
                    return HandleSmallAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return HandleSmallNotAliveRoad(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return HandleSmallNotAliveRoadAndRoof(obstacle, obstacleIndex, obstacles, context, noHit);
                default:
                    return noHit;
            }
        }

        // ----- Обработчики --------------------------------------------------------

        // bigNotAlive -> RoofJump
        private static JumpResolveResult HandleBigNotAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (!IsOverlapAtShift(context, obstacle, context.RoofJumpShift))
                return noHit;

            bool hitSmall = IsHitSmallNotAliveOnRoof(obstacles, context);
            bool hitBigAlive = IsHitBigAliveDuringRoofJump(obstacles, context);
            HamsterStateEnum state = hitSmall || hitBigAlive
                ? HamsterStateEnum.RoofJumpDamage
                : HamsterStateEnum.RoofJump;

            return new JumpResolveResult(state, obstacleIndex);
        }

        // bigAlive -> JumpOnObstacleFromRoof / JumpFromRoofDamage
        private static JumpResolveResult HandleBigAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            // 1. Центр внутри границ препятствия? -> удачный напрыг
            float rightTolerance = context.HamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (IsHamsterCenterInsideObstacleAtShift(
                    context,
                    obstacle,
                    context.JumpFromRoofShift,
                    rightTolerance))
            {
                return new JumpResolveResult(HamsterStateEnum.JumpOnObstacleFromRoof, obstacleIndex);
            }

            // 2. Иначе: есть ли вообще X-пересечение? -> урон
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex);

            // 3. Вообще не задели
            return noHit;
        }

        // smallAlive -> JumpOnObstacleFromRoof / JumpFromRoofDamage
        private static JumpResolveResult HandleSmallAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            // 1. Центр внутри границ препятствия? -> удачный напрыг
            float rightTolerance = context.HamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (IsHamsterCenterInsideObstacleAtShift(
                    context,
                    obstacle,
                    context.JumpFromRoofShift,
                    rightTolerance))
            {
                return new JumpResolveResult(HamsterStateEnum.JumpOnObstacleFromRoof, obstacleIndex);
            }

            // 2. Иначе: есть ли вообще X-пересечение? -> урон
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex);

            // 3. Вообще не задели
            return noHit;
        }

        // smallNotAliveRoad -> JumpFromRoofDamage
        private static JumpResolveResult HandleSmallNotAliveRoad(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex);

            return noHit;
        }

        // smallNotAliveRoadAndRoof -> RoofJump / RoofJumpDamage / JumpFromRoofDamage
        private static JumpResolveResult HandleSmallNotAliveRoadAndRoof(
            JumpObstacleData small,
            int smallIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            bool hasRoofUnderHazard = TryFindRoofUnderRoofHazard(
                small,
                obstacles,
                context,
                out int roofUnderHazardIndex);

            if (hasRoofUnderHazard)
            {
                bool hitSmall = IsHitSmallNotAliveOnRoof(obstacles, context);
                bool hitBigAlive = IsHitBigAliveDuringRoofJump(obstacles, context);
                HamsterStateEnum state = hitSmall || hitBigAlive
                    ? HamsterStateEnum.RoofJumpDamage
                    : HamsterStateEnum.RoofJump;
                return new JumpResolveResult(state, roofUnderHazardIndex);
            }

            // small стоит на дороге: прыгаем с крыши вниз.
            if (IsOverlapAtShift(context, small, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, smallIndex);

            return noHit;
        }

        /// <summary>
        /// Хит-тест smallNotAliveRoadAndRoof на крыше.
        /// Повторяет смысл runtime helper'а CollisionUtils.IsHitSmallNotAliveOnRoof.
        /// </summary>
        private static bool IsHitSmallNotAliveOnRoof(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (!IsSameRuntimeCandidate(obstacle, context))
                    continue;

                if (!IsRoofHazard(obstacle, obstacles, context))
                    continue;

                if (IsOverlapAtShift(context, obstacle, context.RoofJumpShift))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет высокий bigAlive, который roof jump может зацепить перед входом в RoofRun.
        /// </summary>
        private static bool IsHitBigAliveDuringRoofJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (!IsSameRuntimeCandidate(obstacle, context))
                    continue;

                if (obstacle.Type != ObstacleTypeEnum.bigAlive)
                    continue;

                if (IsOverlapAtShift(context, obstacle, context.RoofJumpShift))
                    return true;
            }

            return false;
        }

        private static bool IsRoofHazard(
            JumpObstacleData obstacle,
            IReadOnlyList<JumpObstacleData> allObstacles,
            RoofJumpResolveContext context)
        {
            return TryFindRoofUnderRoofHazard(obstacle, allObstacles, context, out _);
        }

        /// <summary>
        /// Ищет roof obstacle под roof hazard в текущем снимке препятствий.
        /// Повторяет смысл runtime helper'а CollisionUtils.TryFindRoofUnderRoofHazard.
        /// </summary>
        private static bool TryFindRoofUnderRoofHazard(
            JumpObstacleData roofHazard,
            IReadOnlyList<JumpObstacleData> allObstacles,
            RoofJumpResolveContext context,
            out int foundIndex)
        {
            if (roofHazard.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
            {
                foundIndex = NoTarget;
                return false;
            }

            for (int obstacleIndex = 0; obstacleIndex < allObstacles.Count; obstacleIndex++)
            {
                JumpObstacleData candidate = allObstacles[obstacleIndex];
                if (!IsSameRuntimeCandidate(candidate, context))
                    continue;

                if (candidate.IsBottomLine != roofHazard.IsBottomLine)
                    continue;

                if (!CollisionUtils.IsRoofObstacle(candidate.Type))
                    continue;

                if (CollisionUtils.IsOverlap(
                        roofHazard.LeftX,
                        roofHazard.RightX,
                        candidate.LeftX,
                        candidate.RightX))
                {
                    foundIndex = obstacleIndex;
                    return true;
                }
            }

            foundIndex = NoTarget;
            return false;
        }

        private static bool IsSameRuntimeCandidate(
            JumpObstacleData obstacle,
            RoofJumpResolveContext context)
        {
            return obstacle.IsBottomLine == context.IsBottomLine
                   && obstacle.CenterX > context.HamsterCenterX;
        }

        private static bool ShouldBreakByReachRight(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle)
        {
            return obstacle.LeftX - context.ReachShift > context.HamsterRightX + 0.0001f;
        }

        private static bool IsOverlapAtShift(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle,
            float shift)
        {
            return CollisionUtils.IsOverlap(
                context.HamsterLeftX,
                context.HamsterRightX,
                obstacle.LeftX - shift,
                obstacle.RightX - shift);
        }

        private static void LogCandidate(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static void LogResult(
            int obstacleIndex,
            JumpObstacleData obstacle,
            JumpResolveResult result,
            JumpResolveResult noHit,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static void LogBreak(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static bool IsHamsterCenterInsideObstacleAtShift(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle,
            float shift,
            float rightTolerance)
        {
            float left = obstacle.LeftX - shift;
            float right = obstacle.RightX - shift;

            if (obstacle.Type == ObstacleTypeEnum.bigAlive)
            {
                float thirdFraction = (obstacle.RightX - obstacle.LeftX) * 0.3f;
                left -= thirdFraction;
                right += thirdFraction;
            }

            right += rightTolerance;

            return context.HamsterCenterX >= left && context.HamsterCenterX <= right;
        }
    }
}
