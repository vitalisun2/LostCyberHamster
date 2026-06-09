using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Определяет итог супер-прыжка с крыши по тем же правилам, что и runtime super-roof-jump механика.
    /// Resolver не работает с Unity-объектами напрямую: вызывающий код передает снимок препятствий
    /// и геометрию хомяка через <see cref="JumpObstacleData"/> и <see cref="RoofJumpResolveContext"/>.
    /// </summary>
    public static class SuperRoofJumpOutcomeResolver
    {
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // 20 % ширины хомяка
        private const int NoTarget = -1;

        /// <summary>
        /// Обходит препятствия впереди хомяка и через обработчик по типу препятствия
        /// определяет итог super roof jump: прыжок по крыше, супер-сход с крыши, урон или напрыг.
        /// </summary>
        public static JumpResolveResult ResolveSuperRoofJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            JumpResolveResult noHit = new(HamsterStateEnum.SuperJumpFromRoof, NoTarget);
            JumpResolveResult deferredJumpOnObstacle = noHit;
            JumpResolveResult deferredJumpFromRoofDamage = noHit;

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != context.IsBottomLine)
                    continue;

                if (obstacle.CenterX <= context.HamsterCenterX)
                    continue;

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
                if (result.State == HamsterStateEnum.SuperJumpOnObstacleFromRoof)
                {
                    if (deferredJumpOnObstacle.State == noHit.State)
                        deferredJumpOnObstacle = result;

                    LogDeferredJumpOnObstacle(obstacleIndex, obstacle, result, context);
                    continue;
                }

                if (result.State == HamsterStateEnum.SuperJumpFromRoofDamage)
                {
                    if (deferredJumpFromRoofDamage.State == noHit.State)
                        deferredJumpFromRoofDamage = result;

                    LogDeferredDamage(obstacleIndex, obstacle, result, context);
                    continue;
                }

                if (result.State != noHit.State)
                {
                    LogReturn(obstacleIndex, obstacle, result, context);
                    return result;
                }
            }

            if (deferredJumpOnObstacle.State != noHit.State)
            {
                LogDeferredJumpOnObstacleReturn(deferredJumpOnObstacle, context);
                return deferredJumpOnObstacle;
            }

            if (deferredJumpFromRoofDamage.State != noHit.State)
            {
                LogDeferredReturn(deferredJumpFromRoofDamage, context);
                return deferredJumpFromRoofDamage;
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

        // bigNotAlive -> SuperRoofJump / SuperRoofJumpDamage
        private static JumpResolveResult HandleBigNotAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            // проверяем X-перекрытие с учётом worldShift клипа
            bool roofOverlap = IsOverlapAtShift(context, obstacle, context.RoofJumpShift);
            if (!roofOverlap)
            {
                LogRoofHandler(obstacleIndex, obstacle, context, roofOverlap, false, noHit.State);
                return noHit;
            }

            bool hitSmall = IsHitSmallNotAliveOnRoof(obstacles, context);
            bool hitBigAlive = IsHitBigAliveDuringRoofJump(obstacles, context);
            HamsterStateEnum state = hitSmall || hitBigAlive
                ? HamsterStateEnum.SuperRoofJumpDamage
                : HamsterStateEnum.SuperRoofJump;

            LogRoofHandler(obstacleIndex, obstacle, context, roofOverlap, hitSmall, state);

            return new JumpResolveResult(state, obstacleIndex);
        }

        // bigAlive -> SuperJumpOnObstacleFromRoof / SuperJumpFromRoofDamage
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
                return new JumpResolveResult(HamsterStateEnum.SuperJumpOnObstacleFromRoof, obstacleIndex);
            }

            // 2. Иначе: есть ли вообще X-пересечение? -> урон
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.SuperJumpFromRoofDamage, obstacleIndex);

            // 3. Вообще не задели
            return noHit;
        }

        // smallAlive -> SuperJumpOnObstacleFromRoof / SuperJumpFromRoofDamage
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
                return new JumpResolveResult(HamsterStateEnum.SuperJumpOnObstacleFromRoof, obstacleIndex);
            }

            // 2. Иначе: есть ли вообще X-пересечение? -> урон
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.SuperJumpFromRoofDamage, obstacleIndex);

            // 3. Вообще не задели
            return noHit;
        }

        // smallNotAliveRoad -> SuperJumpFromRoofDamage
        private static JumpResolveResult HandleSmallNotAliveRoad(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            bool jumpFromRoofOverlap = IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift);
            LogSmallNotAliveRoadHandler(obstacleIndex, obstacle, context, jumpFromRoofOverlap);

            if (jumpFromRoofOverlap)
                return new JumpResolveResult(HamsterStateEnum.SuperJumpFromRoofDamage, obstacleIndex);

            return noHit;
        }

        // smallNotAliveRoadAndRoof -> SuperRoofJump / SuperRoofJumpDamage / SuperJumpFromRoofDamage
        private static JumpResolveResult HandleSmallNotAliveRoadAndRoof(
            JumpObstacleData small,
            int smallIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            // roof hazard стоит на roof -> прыгаем на крышу
            if (TryFindRoofUnderRoofHazard(small, obstacles, context, out int roofUnderHazardIndex))
            {
                bool hitSmall = IsHitSmallNotAliveOnRoof(obstacles, context);
                bool hitBigAlive = IsHitBigAliveDuringRoofJump(obstacles, context);
                HamsterStateEnum state = hitSmall || hitBigAlive
                    ? HamsterStateEnum.SuperRoofJumpDamage
                    : HamsterStateEnum.SuperRoofJump;

                LogSmallNotAliveRoadAndRoofHandler(
                    smallIndex,
                    small,
                    context,
                    isOnRoof: true,
                    roofUnderHazardIndex,
                    hitSmall,
                    jumpFromRoofOverlap: false,
                    state);

                return new JumpResolveResult(state, roofUnderHazardIndex);
            }

            // иначе проверяем, заденем ли small при "прыжке с крыши"
            bool jumpFromRoofOverlap = IsOverlapAtShift(context, small, context.JumpFromRoofShift);
            LogSmallNotAliveRoadAndRoofHandler(
                smallIndex,
                small,
                context,
                isOnRoof: false,
                NoTarget,
                hitSmall: false,
                jumpFromRoofOverlap,
                jumpFromRoofOverlap ? HamsterStateEnum.SuperJumpFromRoofDamage : noHit.State);

            if (jumpFromRoofOverlap)
                return new JumpResolveResult(HamsterStateEnum.SuperJumpFromRoofDamage, smallIndex);

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
        /// Проверяет высокий bigAlive, который super roof jump может зацепить перед входом в RoofRun.
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

        private static void LogReturn(
            int obstacleIndex,
            JumpObstacleData obstacle,
            JumpResolveResult result,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static void LogDeferredDamage(
            int obstacleIndex,
            JumpObstacleData obstacle,
            JumpResolveResult result,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static void LogDeferredJumpOnObstacle(
            int obstacleIndex,
            JumpObstacleData obstacle,
            JumpResolveResult result,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static void LogDeferredJumpOnObstacleReturn(
            JumpResolveResult result,
            RoofJumpResolveContext context)
        {
            return;
        }

        private static void LogDeferredReturn(
            JumpResolveResult result,
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

        private static void LogRoofHandler(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context,
            bool roofOverlap,
            bool hitSmall,
            HamsterStateEnum state)
        {
            return;
        }

        private static void LogSmallNotAliveRoadHandler(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context,
            bool jumpFromRoofOverlap)
        {
            return;
        }

        private static void LogSmallNotAliveRoadAndRoofHandler(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context,
            bool isOnRoof,
            int roofUnderHazardIndex,
            bool hitSmall,
            bool jumpFromRoofOverlap,
            HamsterStateEnum state)
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
